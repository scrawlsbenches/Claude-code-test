# Distributed Kernel Orchestration System

A production-ready distributed kernel orchestration system for managing hot-swappable kernel modules across distributed node clusters with automated deployment pipelines, canary deployments, and comprehensive observability.

## Features

- **Hot Module Swapping**: Replace modules without downtime
- **Multiple Deployment Strategies**:
  - Direct (Development)
  - Rolling (QA)
  - Blue-Green (Staging)
  - Canary (Production)
- **Distributed Tracing**: OpenTelemetry-based observability
- **Real-time Metrics**: Live performance monitoring
- **Signature Verification**: Cryptographic module validation
- **Automatic Rollback**: Failure detection and recovery
- **REST API**: Complete API for 3rd party integration

## Architecture

```
┌─────────────────────────────────────────────┐
│           REST API Layer                    │
│  - Deployments Controller                   │
│  - Clusters Controller                      │
└─────────────────┬───────────────────────────┘
                  │
┌─────────────────▼───────────────────────────┐
│     Orchestration Layer                     │
│  - DistributedKernelOrchestrator            │
│  - DeploymentPipeline                       │
└─────────────────┬───────────────────────────┘
                  │
┌─────────────────▼───────────────────────────┐
│     Strategy Layer                          │
│  - Direct, Rolling, BlueGreen, Canary       │
└─────────────────┬───────────────────────────┘
                  │
┌─────────────────▼───────────────────────────┐
│     Node Layer                              │
│  - EnvironmentCluster                       │
│  - KernelNode                               │
└─────────────────────────────────────────────┘
```

## Quick Start

### Running with Docker Compose

```bash
# Build and start all services
docker-compose up -d

# View logs
docker-compose logs -f orchestrator-api

# Stop services
docker-compose down
```

**Services:**
- **API**: http://localhost:5000
- **Swagger UI**: http://localhost:5000
- **Jaeger UI**: http://localhost:16686
- **Health**: http://localhost:5000/health

## API Endpoints

### Deployments

```bash
POST /api/v1/deployments          # Create deployment
GET  /api/v1/deployments          # List deployments
GET  /api/v1/deployments/{id}     # Get status
POST /api/v1/deployments/{id}/rollback  # Rollback
```

### Clusters

```bash
GET /api/v1/clusters                    # List clusters
GET /api/v1/clusters/{environment}      # Get cluster info
GET /api/v1/clusters/{environment}/metrics  # Get metrics
```

### Example: Create Deployment

```bash
curl -X POST http://localhost:5000/api/v1/deployments \
  -H "Content-Type: application/json" \
  -d '{
    "moduleName": "payment-processor",
    "version": "2.1.0",
    "targetEnvironment": "Production",
    "requesterEmail": "user@example.com"
  }'
```

## Project Structure

```
Claude-code-test/
├── src/
│   ├── HotSwap.Distributed.Domain/
│   ├── HotSwap.Distributed.Infrastructure/
│   ├── HotSwap.Distributed.Orchestrator/
│   └── HotSwap.Distributed.Api/
├── tests/
├── Dockerfile
├── docker-compose.yml
└── DistributedKernel.sln
```

## License

MIT License
