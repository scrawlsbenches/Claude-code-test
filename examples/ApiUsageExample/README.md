# Distributed Kernel Orchestration API - Usage Examples

A comprehensive example application demonstrating **full utilization** of the Distributed Kernel Orchestration API.

## Overview

This example application showcases all API capabilities including:

- ✅ **All Deployment Strategies** - Direct, Rolling, Blue-Green, and Canary deployments
- ✅ **Cluster Management** - Health checks, node monitoring, and cluster information
- ✅ **Metrics & Monitoring** - Time-series metrics, real-time dashboards
- ✅ **Deployment Lifecycle** - Creation, status tracking, and rollbacks
- ✅ **Error Handling** - Comprehensive error handling and retry logic
- ✅ **Best Practices** - Production-ready patterns for API integration

## Quick Start

### Prerequisites

- .NET 8.0 SDK or later
- Running instance of the Distributed Kernel Orchestration API

### Build and Run

```bash
# Navigate to the examples directory
cd examples/ApiUsageExample

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run with default API URL (http://localhost:5000)
dotnet run

# Run with custom API URL
dotnet run -- http://your-api-url:port
```

### Using Docker

If the API is running via docker-compose:

```bash
# Ensure API is running
cd ../..
docker-compose up -d

# Run the example
cd examples/ApiUsageExample
dotnet run -- http://localhost:5000
```

## Examples Included

### Example 1: Health Check

Verifies API availability and health status.

**Demonstrates:**
- Health endpoint usage
- Basic connectivity testing

**API Endpoint:** `GET /health`

---

### Example 2: List All Clusters

Retrieves a summary of all available clusters.

**Demonstrates:**
- Cluster listing
- Multi-environment overview
- Health status aggregation

**API Endpoint:** `GET /api/v1/clusters`

---

### Example 3: Get Detailed Cluster Information

Retrieves comprehensive information about a specific cluster including nodes and metrics.

**Demonstrates:**
- Detailed cluster queries
- Node-level information
- Current metrics retrieval

**API Endpoint:** `GET /api/v1/clusters/{environment}`

---

### Example 4: Monitor Cluster Metrics

Retrieves time-series metrics for cluster monitoring.

**Demonstrates:**
- Time-series metrics queries
- Custom time ranges
- Interval-based data aggregation
- Multi-environment monitoring

**API Endpoint:** `GET /api/v1/clusters/{environment}/metrics`

**Query Parameters:**
- `from` - Start timestamp (ISO 8601)
- `to` - End timestamp (ISO 8601)
- `interval` - Aggregation interval (1m, 5m, 15m, 1h)

---

### Example 5: Direct Deployment (Development)

Demonstrates the **Direct** deployment strategy for Development environment.

**Deployment Strategy:** All nodes simultaneously (~10 seconds)

**Demonstrates:**
- Development environment deployments
- Fast deployment for testing
- Metadata attachment

**API Endpoint:** `POST /api/v1/deployments`

**Request Body:**
```json
{
  "moduleName": "authentication-service",
  "version": "1.0.0",
  "targetEnvironment": "Development",
  "requesterEmail": "dev@example.com",
  "description": "Initial deployment",
  "requireApproval": false,
  "metadata": {
    "jira-ticket": "AUTH-101"
  }
}
```

---

### Example 6: Track Deployment Status

Polls a deployment's status until completion.

**Demonstrates:**
- Status polling pattern
- Pipeline stage monitoring
- Success/failure detection
- Real-time progress tracking

**API Endpoint:** `GET /api/v1/deployments/{executionId}`

---

### Example 7: Rolling Deployment (QA)

Demonstrates the **Rolling** deployment strategy for QA environment.

**Deployment Strategy:** Sequential batches with health checks (2-5 minutes)

**Demonstrates:**
- QA environment deployments
- Gradual rollout
- Health check validation between batches

**API Endpoint:** `POST /api/v1/deployments`

---

### Example 8: Blue-Green Deployment (Staging)

Demonstrates the **Blue-Green** deployment strategy for Staging environment.

**Deployment Strategy:** Parallel environment with smoke tests (5-10 minutes)

**Demonstrates:**
- Staging environment deployments
- Zero-downtime deployments
- Smoke test integration
- Traffic switching

**API Endpoint:** `POST /api/v1/deployments`

---

### Example 9: Canary Deployment (Production)

Demonstrates the **Canary** deployment strategy for Production environment.

**Deployment Strategy:** Gradual rollout with metrics monitoring (15-30 minutes)

**Demonstrates:**
- Production-safe deployments
- Phased rollout (10% → 30% → 50% → 100%)
- Metrics-based validation
- Automatic rollback on failures

**API Endpoint:** `POST /api/v1/deployments`

**Rollout Phases:**
1. **Phase 1:** 10% of nodes (monitor for 5 minutes)
2. **Phase 2:** 30% of nodes (monitor for 5 minutes)
3. **Phase 3:** 50% of nodes (monitor for 5 minutes)
4. **Phase 4:** 100% of nodes (full deployment)

---

### Example 10: List All Deployments

Retrieves a paginated list of recent deployments.

**Demonstrates:**
- Deployment history
- Status overview
- Execution tracking

**API Endpoint:** `GET /api/v1/deployments`

---

### Example 11: Deployment with Metadata

Creates a deployment with comprehensive metadata and approval requirements.

**Demonstrates:**
- Rich metadata attachment
- Approval workflows
- Change tracking
- Documentation links
- Rollback planning

**Metadata Examples:**
- JIRA tickets
- Severity levels
- Approval information
- Monitoring dashboards
- Runbook links

---

### Example 12: Rollback Deployment

Rolls back a deployment to the previous version.

**Demonstrates:**
- Rollback operations
- Version restoration
- Failure recovery

**API Endpoint:** `POST /api/v1/deployments/{executionId}/rollback`

---

### Example 13: Multi-Environment Health Dashboard

Creates a real-time health dashboard across all environments.

**Demonstrates:**
- Multi-environment monitoring
- Aggregate health views
- Visual progress bars
- Comparative analysis

**Features:**
- Environment health percentages
- CPU and memory usage bars
- Latency and error rates
- Request throughput (RPS)

---

### Example 14: Error Handling

Demonstrates proper error handling for various failure scenarios.

**Demonstrates:**
- Invalid deployment ID handling
- Non-existent cluster queries
- Validation error handling
- Graceful degradation
- Retry logic

**Test Cases:**
1. Query non-existent deployment
2. Query non-existent cluster
3. Create deployment with invalid data

---

## Project Structure

```
ApiUsageExample/
├── Client/
│   └── DistributedKernelApiClient.cs  # API client wrapper with retry logic
├── Models/
│   └── ApiModels.cs                   # Request/response models
├── Program.cs                          # Main example program
├── ApiUsageExample.csproj             # Project file
└── README.md                          # This file
```

## API Client Features

The `DistributedKernelApiClient` class provides:

### Deployment Operations
- `CreateDeploymentAsync()` - Create and start a new deployment
- `GetDeploymentStatusAsync()` - Get deployment status and results
- `ListDeploymentsAsync()` - List recent deployments
- `RollbackDeploymentAsync()` - Rollback a deployment

### Cluster Operations
- `ListClustersAsync()` - List all clusters
- `GetClusterInfoAsync()` - Get detailed cluster information
- `GetClusterMetricsAsync()` - Get time-series metrics

### Health Operations
- `CheckHealthAsync()` - Check API health status

### Built-in Features
- ✅ **Automatic retry logic** - Handles transient failures (max 3 retries)
- ✅ **Exponential backoff** - Progressive delay between retries
- ✅ **Comprehensive logging** - Detailed diagnostics via ILogger
- ✅ **Type-safe models** - Strong typing for all requests/responses
- ✅ **Async/await patterns** - Non-blocking I/O operations
- ✅ **Proper disposal** - IDisposable implementation

## Environment Variables

Configure the API base URL via environment variable:

```bash
# Linux/macOS
export API_BASE_URL=http://localhost:5000

# Windows (PowerShell)
$env:API_BASE_URL="http://localhost:5000"

# Windows (CMD)
set API_BASE_URL=http://localhost:5000
```

Or pass it as a command-line argument:

```bash
dotnet run -- http://your-api-url:port
```

## Configuration

### HTTP Client Configuration

The HTTP client is configured with:
- **Base Address:** API endpoint URL
- **Timeout:** 5 minutes (for long-running deployments)
- **Retry Logic:** 3 attempts with 1-second intervals

### Logging Configuration

Logging is configured to:
- **Provider:** Console
- **Level:** Information and above
- **Format:** Structured logs with timestamps

## Deployment Strategies Summary

| Environment | Strategy   | Nodes | Duration     | Use Case                |
|-------------|-----------|-------|--------------|-------------------------|
| Development | Direct    | 3     | ~10 seconds  | Fast iteration, testing |
| QA          | Rolling   | 5     | 2-5 minutes  | Validation, integration |
| Staging     | Blue-Green| 10    | 5-10 minutes | Pre-production testing  |
| Production  | Canary    | 20    | 15-30 minutes| Safe production rollout |

## Best Practices Demonstrated

### 1. Error Handling
- Try-catch blocks around API calls
- Specific exception types for different errors
- Graceful degradation on failures
- User-friendly error messages

### 2. Retry Logic
- Automatic retries for transient failures
- Exponential backoff to prevent overwhelming the API
- Configurable retry limits

### 3. Status Polling
- Periodic polling for long-running operations
- Timeout mechanisms
- Early exit on completion

### 4. Logging
- Structured logging with context
- Different log levels (Info, Warning, Error)
- Correlation IDs for distributed tracing

### 5. Resource Management
- IDisposable pattern for HTTP client
- Async/await for non-blocking operations
- Proper cancellation token usage

## Troubleshooting

### API Not Responding

```
Error: Connection refused to http://localhost:5000
```

**Solution:** Ensure the API is running:
```bash
cd ../..
docker-compose up -d
# or
dotnet run --project src/HotSwap.Distributed.Api
```

### Deployment Not Found

```
Error: Deployment {id} not found
```

**Solution:** Wait a few seconds after creating a deployment before querying its status. The deployment may be initializing.

### Timeout Errors

```
Error: The request timed out
```

**Solution:** Increase the HTTP client timeout or check API performance:
```csharp
client.Timeout = TimeSpan.FromMinutes(10);
```

## Performance Considerations

### API Call Latency

Expected latencies:
- **Health checks:** <100ms
- **Deployment creation:** <500ms
- **Metrics retrieval:** <200ms (cached)
- **Status queries:** <300ms

### Polling Intervals

Recommended polling intervals for deployment status:
- **Development:** 2-3 seconds
- **QA/Staging:** 5-10 seconds
- **Production:** 10-15 seconds

## Security Considerations

### Production Deployment

For production use:

1. **Enable authentication:** Add JWT tokens to requests
2. **Use HTTPS:** Configure TLS/SSL certificates
3. **Validate inputs:** Sanitize all user inputs
4. **Rate limiting:** Implement client-side rate limiting
5. **Secrets management:** Use secure configuration for API URLs

Example with authentication:
```csharp
services.AddHttpClient<DistributedKernelApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
});
```

## Advanced Usage

### Custom Retry Policy

Implement custom retry logic:

```csharp
var policy = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(
        retryCount: 5,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (exception, timespan, context) =>
        {
            logger.LogWarning("Retry {RetryCount} after {Delay}s",
                context.Count, timespan.TotalSeconds);
        });
```

### Parallel Deployments

Deploy to multiple environments concurrently:

```csharp
var deploymentTasks = new[]
{
    CreateDeploymentAsync("Development"),
    CreateDeploymentAsync("QA"),
    CreateDeploymentAsync("Staging")
};

var results = await Task.WhenAll(deploymentTasks);
```

### WebSocket Support (Future)

For real-time deployment updates, consider implementing WebSocket support:

```csharp
// Future enhancement - not yet implemented
await client.SubscribeToDeploymentUpdatesAsync(executionId, update =>
{
    Console.WriteLine($"Update: {update.Stage} - {update.Status}");
});
```

## Contributing

Improvements to these examples are welcome! Consider adding:

- Additional error scenarios
- Performance benchmarking
- Load testing examples
- Integration tests
- CI/CD pipeline integration

## License

MIT License - See [LICENSE](../../LICENSE) for details

## Related Documentation

- [Main README](../../README.md) - Project overview
- [API Documentation](http://localhost:5000/swagger) - Interactive API docs
- [Testing Guide](../../TESTING.md) - Testing documentation
- [Project Status](../../PROJECT_STATUS_REPORT.md) - Production readiness

## Support

For issues or questions:
- Check the [API documentation](http://localhost:5000/swagger)
- Review the [troubleshooting guide](#troubleshooting)
- Open an issue on GitHub

---

**Last Updated:** November 14, 2025
**Version:** 1.0.0
**Compatibility:** Distributed Kernel Orchestration API v1
