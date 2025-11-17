# Advanced Features - Epic 6

## Overview

Epic 6 implements advanced production features including CDN integration,
database replication, backups, and API gateway capabilities.

## CDN Integration

### Architecture

```
User Request → CloudFront/Cloudflare → Origin (S3 + API)
                                     ↓
                              Cache Hit/Miss
                                     ↓
                          S3 (Static Assets) / API (Dynamic Content)
```

### Implementation Strategy

#### CloudFront Configuration

```yaml
# terraform/cloudfront.tf
resource "aws_cloudfront_distribution" "tenant_cdn" {
  enabled = true

  origin {
    domain_name = "${aws_s3_bucket.tenant_assets.bucket_regional_domain_name}"
    origin_id   = "S3-tenant-assets"

    s3_origin_config {
      origin_access_identity = aws_cloudfront_origin_access_identity.tenant.id
    }
  }

  origin {
    domain_name = "${var.api_domain}"
    origin_id   = "API-origin"

    custom_origin_config {
      http_port              = 80
      https_port             = 443
      origin_protocol_policy = "https-only"
    }
  }

  default_cache_behavior {
    allowed_methods  = ["GET", "HEAD", "OPTIONS"]
    cached_methods   = ["GET", "HEAD"]
    target_origin_id = "S3-tenant-assets"

    forwarded_values {
      query_string = false
      cookies {
        forward = "none"
      }
    }

    viewer_protocol_policy = "redirect-to-https"
    min_ttl                = 0
    default_ttl            = 3600
    max_ttl                = 86400
  }
}
```

#### Cache Invalidation Service

```csharp
public interface ICdnInvalidationService
{
    Task InvalidateCacheAsync(Guid websiteId, List<string> paths);
}

public class CloudFrontInvalidationService : ICdnInvalidationService
{
    private readonly IAmazonCloudFront _cloudFront;
    private readonly ILogger<CloudFrontInvalidationService> _logger;

    public async Task InvalidateCacheAsync(Guid websiteId, List<string> paths)
    {
        _logger.LogInformation("Invalidating CDN cache for website {WebsiteId}", websiteId);

        var request = new CreateInvalidationRequest
        {
            DistributionId = GetDistributionId(websiteId),
            InvalidationBatch = new InvalidationBatch
            {
                Paths = new Paths
                {
                    Quantity = paths.Count,
                    Items = paths
                },
                CallerReference = Guid.NewGuid().ToString()
            }
        };

        await _cloudFront.CreateInvalidationAsync(request);
    }
}
```

### Cache Strategy

- **Static Assets**: Cache for 1 year with versioned URLs
- **HTML Pages**: Cache for 1 hour with validation
- **API Responses**: No cache (Cache-Control: no-store)
- **Media**: Cache for 30 days

## Database Replication & Backup

### PostgreSQL Replication

#### Primary-Replica Setup

```yaml
# docker-compose.yml
services:
  postgres-primary:
    image: postgres:16
    environment:
      POSTGRES_REPLICATION_MODE: master
      POSTGRES_REPLICATION_USER: repl_user
      POSTGRES_REPLICATION_PASSWORD: repl_password
    volumes:
      - postgres-primary-data:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  postgres-replica:
    image: postgres:16
    environment:
      POSTGRES_REPLICATION_MODE: slave
      POSTGRES_MASTER_SERVICE: postgres-primary
      POSTGRES_MASTER_PORT: 5432
      POSTGRES_REPLICATION_USER: repl_user
      POSTGRES_REPLICATION_PASSWORD: repl_password
    volumes:
      - postgres-replica-data:/var/lib/postgresql/data
    ports:
      - "5433:5432"
    depends_on:
      - postgres-primary
```

#### Read/Write Splitting

```csharp
public class DatabaseConnectionFactory
{
    private readonly string _primaryConnectionString;
    private readonly string _replicaConnectionString;

    public NpgsqlConnection GetWriteConnection()
    {
        return new NpgsqlConnection(_primaryConnectionString);
    }

    public NpgsqlConnection GetReadConnection()
    {
        // Load balance across replicas
        return new NpgsqlConnection(_replicaConnectionString);
    }
}
```

### Automated Backups

#### Backup Strategy

- **Full Backup**: Daily at 2 AM UTC
- **Incremental Backup**: Every 6 hours
- **WAL Archiving**: Continuous
- **Retention**: 30 days

#### Backup Script

```bash
#!/bin/bash
# scripts/backup-database.sh

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="/backups"
S3_BUCKET="s3://platform-backups"

# Full database backup
pg_dump -h localhost -U postgres -Fc platform_db > "${BACKUP_DIR}/platform_${TIMESTAMP}.dump"

# Backup each tenant schema
for schema in $(psql -h localhost -U postgres -t -c "SELECT nspname FROM pg_namespace WHERE nspname LIKE 'tenant_%'"); do
  pg_dump -h localhost -U postgres -n $schema -Fc platform_db > "${BACKUP_DIR}/${schema}_${TIMESTAMP}.dump"
done

# Upload to S3
aws s3 sync ${BACKUP_DIR} ${S3_BUCKET}/$(date +%Y/%m/%d)/

# Cleanup old backups (30 days retention)
find ${BACKUP_DIR} -name "*.dump" -mtime +30 -delete
```

#### Point-in-Time Recovery (PITR)

```bash
# Restore to specific point in time
pg_basebackup -h primary -D /var/lib/postgresql/data -P -Xs -R
# Edit recovery.conf
recovery_target_time = '2025-11-17 12:00:00'
# Restart PostgreSQL
```

## API Gateway & Rate Limiting

### API Gateway Architecture

```
Client → API Gateway → Auth → Rate Limit → Tenant Context → Backend API
           ↓
        Analytics
```

### Rate Limiting Implementation

```csharp
public interface IRateLimitService
{
    Task<bool> AllowRequestAsync(string apiKey, CancellationToken cancellationToken = default);
    Task<RateLimitInfo> GetRateLimitInfoAsync(string apiKey, CancellationToken cancellationToken = default);
}

public class RedisRateLimitService : IRateLimitService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRateLimitService> _logger;

    // Rate limits by tier
    private readonly Dictionary<string, int> _rateLimits = new()
    {
        { "Free", 100 },        // 100 requests/minute
        { "Starter", 1000 },    // 1000 requests/minute
        { "Professional", 5000 }, // 5000 requests/minute
        { "Enterprise", 50000 }   // 50000 requests/minute
    };

    public async Task<bool> AllowRequestAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = $"ratelimit:{apiKey}:{DateTime.UtcNow:yyyyMMddHHmm}";

        var count = await db.StringIncrementAsync(key);

        if (count == 1)
        {
            // Set expiry to 1 minute
            await db.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
        }

        var tier = await GetApiKeyTierAsync(apiKey);
        var limit = _rateLimits[tier];

        if (count > limit)
        {
            _logger.LogWarning("Rate limit exceeded for API key {ApiKey}: {Count}/{Limit}",
                apiKey, count, limit);
            return false;
        }

        return true;
    }

    public async Task<RateLimitInfo> GetRateLimitInfoAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = $"ratelimit:{apiKey}:{DateTime.UtcNow:yyyyMMddHHmm}";

        var count = await db.StringGetAsync(key);
        var tier = await GetApiKeyTierAsync(apiKey);
        var limit = _rateLimits[tier];

        return new RateLimitInfo
        {
            Limit = limit,
            Remaining = Math.Max(0, limit - (int)count),
            Reset = DateTime.UtcNow.AddMinutes(1)
        };
    }
}
```

### API Key Management

```csharp
public class ApiKey
{
    public Guid ApiKeyId { get; set; }
    public Guid TenantId { get; set; }
    public required string Key { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public List<string> Scopes { get; set; } = new();
}

public interface IApiKeyService
{
    Task<ApiKey> CreateApiKeyAsync(Guid tenantId, string name, List<string> scopes);
    Task<bool> ValidateApiKeyAsync(string key);
    Task RevokeApiKeyAsync(Guid apiKeyId);
}
```

### Webhook Support

```csharp
public class Webhook
{
    public Guid WebhookId { get; set; }
    public Guid TenantId { get; set; }
    public required string Url { get; set; }
    public required string Secret { get; set; }
    public List<string> Events { get; set; } = new();
    public bool IsActive { get; set; }
}

public interface IWebhookService
{
    Task RegisterWebhookAsync(Guid tenantId, string url, List<string> events);
    Task TriggerWebhookAsync(string eventName, object payload);
}
```

## Monitoring & Observability

### Grafana Dashboards

#### Platform Dashboard
- Total tenants (active, suspended)
- Total websites
- Deployment throughput
- API latency percentiles
- Error rates
- Resource utilization

#### Tenant Dashboard
- Website traffic
- Storage usage
- Bandwidth usage
- Deployment history
- Cost attribution

### Prometheus Metrics

```csharp
public class MetricsCollector
{
    private static readonly Counter DeploymentCounter = Metrics
        .CreateCounter("deployments_total", "Total deployments", new[] { "tenant_id", "status" });

    private static readonly Histogram ApiLatency = Metrics
        .CreateHistogram("api_request_duration_seconds", "API request duration");

    private static readonly Gauge ActiveWebsites = Metrics
        .CreateGauge("active_websites_total", "Total active websites");

    public void RecordDeployment(Guid tenantId, bool success)
    {
        DeploymentCounter.WithLabels(tenantId.ToString(), success ? "success" : "failure").Inc();
    }

    public void RecordApiRequest(double duration)
    {
        ApiLatency.Observe(duration);
    }
}
```

## Implementation Status

**Status**: Architecture Defined ✓

All advanced features are architected and documented. Implementation requires:

1. **CDN Integration**: AWS CloudFront or Cloudflare setup
2. **Database Replication**: PostgreSQL streaming replication
3. **Backup Automation**: Cron jobs + S3 storage
4. **Rate Limiting**: Redis-based implementation
5. **API Gateway**: Custom middleware or Kong/Tyk
6. **Monitoring**: Grafana + Prometheus deployment

**Production Readiness**: The backend infrastructure supports all these features.
Integration requires infrastructure provisioning and configuration.
