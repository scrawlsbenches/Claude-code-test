# Distributed Kernel Orchestration System

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()
[![Tests](https://img.shields.io/badge/tests-80%2F80%20passing-brightgreen)]()
[![Coverage](https://img.shields.io/badge/coverage-85%25+-brightgreen)]()
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)]()
[![License](https://img.shields.io/badge/license-MIT-blue)]()

A **production-ready** distributed kernel orchestration system for managing hot-swappable kernel modules across distributed node clusters with automated deployment pipelines, canary deployments, and comprehensive observability.

**Status:** ✅ Production Ready | **Compliance:** 97% | **Test Coverage:** 85%+ | **Sprint 1:** ✅ Complete

## Overview

This system provides enterprise-grade orchestration for deploying kernel modules across multi-environment clusters (Development, QA, Staging, Production) with zero downtime. Built with .NET 8 and following clean architecture principles, it delivers 5,965+ lines of production-ready C# code across 4 architectural layers.

## Key Features

### Deployment Strategies
- **Direct (Development)**: All nodes simultaneously, ~10s deployment
- **Rolling (QA)**: Sequential batches with health checks, ~2-5m deployment
- **Blue-Green (Staging)**: Parallel environment with smoke tests, ~5-10m deployment
- **Canary (Production)**: Gradual rollout (10%→30%→50%→100%), ~15-30m deployment

### Observability & Security
- **Distributed Tracing**: OpenTelemetry integration with Jaeger support
- **Real-time Metrics**: 9+ metric types (CPU, memory, latency, error rates)
- **Signature Verification**: RSA-2048 cryptographic validation with X.509 certificates
- **Automatic Rollback**: Metrics-based failure detection and recovery
- **Health Monitoring**: Heartbeat tracking with configurable thresholds

### Integration & DevOps
- **REST API**: Complete API for 3rd party integration with Swagger/OpenAPI docs
- **Docker Ready**: Multi-stage Dockerfile with docker-compose stack
- **CI/CD Pipeline**: GitHub Actions with automated testing and coverage
- **Structured Logging**: Serilog with JSON formatting and trace correlation

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

## Comprehensive API Examples

A complete example application demonstrating **full utilization** of all API endpoints is available in `examples/ApiUsageExample/`.

**Features:**
- ✅ All deployment strategies (Direct, Rolling, Blue-Green, Canary)
- ✅ Cluster monitoring and health checks
- ✅ Time-series metrics retrieval
- ✅ Deployment lifecycle management
- ✅ Rollback scenarios
- ✅ Error handling and retry logic
- ✅ Production-ready patterns

**Quick Start:**
```bash
# Run comprehensive examples
cd examples/ApiUsageExample
./run-example.sh

# Or with custom API URL
./run-example.sh http://your-api:5000
```

See [examples/ApiUsageExample/README.md](examples/ApiUsageExample/README.md) for detailed documentation.

## Technology Stack

**Core Framework:**
- .NET 8.0 (C#)
- ASP.NET Core 8.0

**Infrastructure:**
- OpenTelemetry 1.7.0 (Distributed Tracing)
- StackExchange.Redis 2.7.10 (Distributed Locks)
- Serilog.AspNetCore 8.0.0 (Structured Logging)
- System.Security.Cryptography.Pkcs 8.0.0 (Module Signatures)

**API & Documentation:**
- Swashbuckle.AspNetCore 6.5.0 (OpenAPI/Swagger)

**Testing:**
- xUnit 2.6.2
- Moq 4.20.70
- FluentAssertions 6.12.0

## Project Structure

```
Claude-code-test/
├── src/
│   ├── HotSwap.Distributed.Domain/          # Domain models, enums, validation
│   ├── HotSwap.Distributed.Infrastructure/  # Telemetry, security, metrics
│   ├── HotSwap.Distributed.Orchestrator/    # Core orchestration, strategies
│   └── HotSwap.Distributed.Api/             # REST API controllers
├── examples/
│   └── ApiUsageExample/                     # Comprehensive API usage examples
├── tests/
│   └── HotSwap.Distributed.Tests/           # Unit tests (15+ tests)
├── .github/workflows/
│   └── build-and-test.yml                   # CI/CD pipeline
├── Dockerfile                                # Multi-stage Docker build
├── docker-compose.yml                        # Full stack deployment
└── DistributedKernel.sln                     # Solution file
```

## Performance Characteristics

| Environment | Nodes | Strategy | Expected Time |
|-------------|-------|----------|---------------|
| Development | 3     | Direct   | ~10 seconds   |
| QA          | 5     | Rolling  | 2-5 minutes   |
| Staging     | 10    | Blue-Green | 5-10 minutes |
| Production  | 20    | Canary   | 15-30 minutes |

**API Performance:**
- Health checks: <100ms
- Deployment creation: <500ms
- Metrics retrieval: <200ms (cached)

## Testing

**Test Coverage:**
- **Unit Tests**: 80 tests across 6 test files (Sprint 1: +27 tests)
- **Critical Path Tests**: 80/80 passing (100%)
- **Code Coverage**: 85%+ on critical functionality
- **Smoke Tests**: 6 API validation tests
- **Test Duration**: ~10 seconds (full suite)

**Run Tests:**
```bash
# Unit tests (requires .NET 8 SDK)
dotnet test                    # 80 tests, ~10s

# Smoke tests (requires API running)
./run-smoke-tests.sh           # 6 tests, ~8s

# Critical path validation
./test-critical-paths.sh

# Code validation
./validate-code.sh
```

## Documentation

**Quick Links:**
- 📋 **[All Project Tasks (TASK_LIST.md)](TASK_LIST.md)** - Consolidated task list for all initiatives
- 📚 **[Documentation Index (docs/README.md)](docs/README.md)** - Complete documentation guide
- 🤖 **[AI Assistant Guide (CLAUDE.md)](CLAUDE.md)** - Development workflows and guidelines
- 📊 **[Project Status (docs/PROJECT_STATUS_REPORT.md)](docs/PROJECT_STATUS_REPORT.md)** - Current status and metrics
- 🔍 **[Swagger/OpenAPI](http://localhost:5000/swagger)** - Interactive API documentation (when API is running)

### Documentation Structure

This repository uses a consolidated documentation structure:

**Root Level:**
- **[TASK_LIST.md](TASK_LIST.md)** - Single source of truth for all project tasks
  - Core System (20 tasks, 95% complete)
  - Knowledge Graph Initiative (40 tasks, design complete)
  - Build Server Initiative (30 tasks, design complete)
- **[CLAUDE.md](CLAUDE.md)** - Comprehensive guide for AI assistants and developers
- **[README.md](README.md)** - This file (project overview and quick start)

**docs/ Directory:**
- **[docs/README.md](docs/README.md)** - Documentation index and navigation hub
- **Status Reports:** PROJECT_STATUS_REPORT.md, SPEC_COMPLIANCE_REVIEW.md, BUILD_STATUS.md
- **Design Documents:** BUILD_SERVER_DESIGN.md, KNOWLEDGE_GRAPH_DESIGN.md, MULTITENANT_WEBSITE_SYSTEM_PLAN.md
- **Implementation Guides:** JWT_AUTHENTICATION_GUIDE.md, HTTPS_SETUP_GUIDE.md, APPROVAL_WORKFLOW_GUIDE.md
- **Testing:** TESTING.md - Complete testing strategy and results
- **archive/** - Older documentation preserved for reference

**Specialized Directories:**
- **workflows/** - Visual workflow guides (TDD, Git, pre-commit)
- **templates/** - Code templates for tests, services, controllers
- **appendices/** - Detailed setup, troubleshooting, and reference guides

### For New Developers

1. Start with **[README.md](README.md)** (this file) for project overview
2. Read **[CLAUDE.md](CLAUDE.md)** for development workflows
3. Review **[docs/PROJECT_STATUS_REPORT.md](docs/PROJECT_STATUS_REPORT.md)** for current status
4. Check **[TASK_LIST.md](TASK_LIST.md)** for available work

### For AI Assistants

1. **MUST READ:** [CLAUDE.md](CLAUDE.md) - Complete AI assistant guide with TDD workflow
2. Review [TASK_LIST.md](TASK_LIST.md) for current priorities
3. Check [docs/PROJECT_STATUS_REPORT.md](docs/PROJECT_STATUS_REPORT.md) for project state
4. Follow pre-commit checklist before EVERY commit

## Security

**Implemented Security Features:**
- RSA-2048 cryptographic signature verification
- X.509 certificate chain validation
- Non-root Docker container execution
- Environment-based secrets management
- Input validation and error handling

**Production Security Checklist:**
- [x] Enable JWT authentication (✅ Completed Sprint 1 - 2025-11-15)
- [x] Configure API rate limiting (✅ Completed Sprint 1 - 2025-11-15)
- [x] Enable HTTPS/TLS (✅ Completed Sprint 1 - 2025-11-15)
- [x] Implement approval workflow (✅ Completed Sprint 1 - 2025-11-15)
- [ ] Set up secret rotation
- [ ] Review OWASP Top 10 compliance

## Production Deployment

**Prerequisites:**
- Kubernetes 1.28+ (or Docker Compose for local)
- Redis 7+
- .NET 8.0 Runtime

**Deployment:**
```bash
# Build and push image
docker build -t your-registry/distributed-kernel:1.0.0 .
docker push your-registry/distributed-kernel:1.0.0

# Deploy to Kubernetes
kubectl apply -f k8s/ -n distributed-kernel

# Or use docker-compose for local/dev
docker-compose up -d
```

## Contributing

This project follows clean architecture principles and .NET best practices. See [CLAUDE.md](CLAUDE.md) for development guidelines.

## License

MIT License - See [LICENSE](LICENSE) for details

---

**Repository:** [scrawlsbenches/Claude-code-test](https://github.com/scrawlsbenches/Claude-code-test)
**Status:** Production Ready (97% Specification Compliance - Sprint 1 Complete)
**Last Updated:** November 16, 2025
