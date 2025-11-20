# Advanced Features - Epic 6

## Overview

Epic 6 implements advanced production features including CDN integration,
database replication, backups, and API gateway capabilities.

## CDN Integration

### Architecture

```
User Request → Nginx/Varnish Cache → Origin (MinIO + API)
                                   ↓
                            Cache Hit/Miss
                                   ↓
                       MinIO (Static Assets) / API (Dynamic Content)
```

### Implementation Strategy

#### Nginx Caching Proxy Configuration

```nginx
# /etc/nginx/conf.d/cdn-cache.conf

# Define cache zones
proxy_cache_path /var/cache/nginx/static levels=1:2 keys_zone=static_cache:100m max_size=10g inactive=7d use_temp_path=off;
proxy_cache_path /var/cache/nginx/media levels=1:2 keys_zone=media_cache:100m max_size=50g inactive=30d use_temp_path=off;

upstream minio_backend {
    server minio.example.com:9000;
}

upstream api_backend {
    server api.example.com:5000;
}

server {
    listen 80;
    listen 443 ssl http2;
    server_name cdn.example.com;

    ssl_certificate /etc/nginx/ssl/cert.pem;
    ssl_certificate_key /etc/nginx/ssl/key.pem;

    # Redirect HTTP to HTTPS
    if ($scheme != "https") {
        return 301 https://$host$request_uri;
    }

    # Static assets from MinIO
    location /assets/ {
        proxy_pass https://minio_backend/tenant-assets/;
        proxy_cache static_cache;
        proxy_cache_valid 200 1y;
        proxy_cache_valid 404 10m;
        proxy_cache_key "$scheme$request_method$host$request_uri";
        add_header X-Cache-Status $upstream_cache_status;
        add_header Cache-Control "public, max-age=31536000, immutable";
    }

    # Media files from MinIO
    location /media/ {
        proxy_pass https://minio_backend/tenant-media/;
        proxy_cache media_cache;
        proxy_cache_valid 200 30d;
        proxy_cache_valid 404 10m;
        add_header X-Cache-Status $upstream_cache_status;
        add_header Cache-Control "public, max-age=2592000";
    }

    # API requests (no caching)
    location /api/ {
        proxy_pass https://api_backend/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        add_header Cache-Control "no-store, no-cache, must-revalidate";
    }
}
```

#### Cache Invalidation Service

```csharp
public interface ICacheInvalidationService
{
    Task InvalidateCacheAsync(Guid websiteId, List<string> paths);
}

public class NginxCacheInvalidationService : ICacheInvalidationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NginxCacheInvalidationService> _logger;
    private readonly string _nginxPurgeEndpoint;

    public async Task InvalidateCacheAsync(Guid websiteId, List<string> paths)
    {
        _logger.LogInformation("Invalidating Nginx cache for website {WebsiteId}", websiteId);

        var httpClient = _httpClientFactory.CreateClient();

        // Option 1: Use nginx_cache_purge module
        // Send PURGE requests to Nginx with special header
        foreach (var path in paths)
        {
            var purgeUrl = $"{_nginxPurgeEndpoint}{path}";
            var request = new HttpRequestMessage(new HttpMethod("PURGE"), purgeUrl);
            await httpClient.SendAsync(request);
        }

        // Option 2: Delete cache files directly from filesystem
        // var cacheDir = "/var/cache/nginx/static";
        // foreach (var path in paths)
        // {
        //     var cacheKey = GenerateCacheKey(path);
        //     var cacheFile = Path.Combine(cacheDir, cacheKey);
        //     if (File.Exists(cacheFile))
        //         File.Delete(cacheFile);
        // }
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
MINIO_BUCKET="platform-backups"
MINIO_ALIAS="minio-prod"

# Full database backup
pg_dump -h localhost -U postgres -Fc platform_db > "${BACKUP_DIR}/platform_${TIMESTAMP}.dump"

# Backup each tenant schema
for schema in $(psql -h localhost -U postgres -t -c "SELECT nspname FROM pg_namespace WHERE nspname LIKE 'tenant_%'"); do
  pg_dump -h localhost -U postgres -n $schema -Fc platform_db > "${BACKUP_DIR}/${schema}_${TIMESTAMP}.dump"
done

# Upload to MinIO using mc (MinIO client)
# Configure MinIO alias: mc alias set minio-prod https://minio.example.com:9000 ACCESS_KEY SECRET_KEY
mc mirror ${BACKUP_DIR} ${MINIO_ALIAS}/${MINIO_BUCKET}/$(date +%Y/%m/%d)/

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

1. **CDN Integration**: Nginx or Varnish caching proxy setup
2. **Database Replication**: PostgreSQL streaming replication
3. **Backup Automation**: Cron jobs + MinIO object storage
4. **Rate Limiting**: Redis-based implementation
5. **API Gateway**: Custom middleware or Kong/Tyk
6. **Monitoring**: Grafana + Prometheus deployment

**Production Readiness**: The backend infrastructure supports all these features.
Integration requires infrastructure provisioning and configuration.
