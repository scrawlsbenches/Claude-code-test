# Distributed Kernel Orchestration System

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()
[![Tests](https://img.shields.io/badge/tests-582%20total%20(568%20passing%2C%2014%20skipped)-brightgreen)]()
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
- **Unit Tests**: 582 tests (568 passing, 14 skipped, 0 failed)
- **Critical Path Tests**: 568/568 passing (100%)
- **Code Coverage**: 85%+ on critical functionality
- **Smoke Tests**: 6 API validation tests
- **Test Duration**: ~18 seconds (full suite)

**Run Tests:**
```bash
# Unit tests (requires .NET 8 SDK)
dotnet test                    # 582 tests, ~18s

# Smoke tests (requires API running)
./run-smoke-tests.sh           # 6 tests, ~8s

# Critical path validation
./test-critical-paths.sh

# Code validation
./validate-code.sh
```

## Documentation

This repository includes comprehensive documentation organized into multiple sections:

### Core Documentation
- **[README.md](README.md)** - Quick start and overview (this file)
- **[CLAUDE.md](CLAUDE.md)** - Development guidelines for AI assistants
- **[TESTING.md](TESTING.md)** - Complete testing guide and procedures
- **[PROJECT_STATUS_REPORT.md](PROJECT_STATUS_REPORT.md)** - Production readiness status
- **[SPEC_COMPLIANCE_REVIEW.md](SPEC_COMPLIANCE_REVIEW.md)** - Specification compliance analysis
- **[BUILD_STATUS.md](BUILD_STATUS.md)** - Build validation report
- **Swagger/OpenAPI** - Interactive API documentation at `/swagger`

### Workflows & Guides
- **[workflows/pre-commit-checklist.md](workflows/pre-commit-checklist.md)** - Visual pre-commit workflow with Mermaid diagrams
- **[workflows/tdd-workflow.md](workflows/tdd-workflow.md)** - Test-Driven Development Red-Green-Refactor cycle
- **[workflows/git-workflow.md](workflows/git-workflow.md)** - Git conventions and branching strategy
- **[workflows/task-management.md](workflows/task-management.md)** - Using TASK_LIST.md for project planning

### Code Templates
- **[templates/test-template.cs](templates/test-template.cs)** - Comprehensive unit test template with xUnit and FluentAssertions
- **[templates/service-template.cs](templates/service-template.cs)** - Service layer implementation template with DI and async patterns
- **[templates/controller-template.cs](templates/controller-template.cs)** - REST API controller template with validation and error handling

### Detailed Appendices
- **[appendices/A-DETAILED-SETUP.md](appendices/A-DETAILED-SETUP.md)** - Step-by-step development environment setup
- **[appendices/B-NO-SDK-CHECKLIST.md](appendices/B-NO-SDK-CHECKLIST.md)** - Development workflow without local .NET SDK
- **[appendices/C-STALE-DOCS-GUIDE.md](appendices/C-STALE-DOCS-GUIDE.md)** - Preventing stale documentation
- **[appendices/D-TDD-PATTERNS.md](appendices/D-TDD-PATTERNS.md)** - Advanced Test-Driven Development patterns
- **[appendices/E-TROUBLESHOOTING.md](appendices/E-TROUBLESHOOTING.md)** - Comprehensive troubleshooting guide
- **[appendices/F-CHANGELOG.md](appendices/F-CHANGELOG.md)** - Complete changelog history

### Automation Scripts
- **update-docs-metrics.sh** - Auto-update documentation metrics (test count, .NET version)
- **docs-validation.sh** - Validate documentation freshness and accuracy
- **pre-commit-docs-reminder.sh** - Pre-commit documentation reminders
- **hooks/pre-commit** - Git pre-commit hook for documentation validation

**Usage:**
```bash
# Update documentation metrics automatically
./update-docs-metrics.sh --yes

# Validate documentation freshness
./docs-validation.sh

# Install pre-commit hook
cp hooks/pre-commit .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit
```

### Claude Skills

**[SKILLS.md](SKILLS.md)** - 8 automated workflow skills for AI-assisted development (~3,700 lines)

This project includes specialized Claude Skills that automate complex development workflows, enforce best practices, and prevent common errors. These skills guide AI assistants through systematic processes for setup, testing, validation, and maintenance.

**Available Skills:**

| Skill | Purpose | When to Use |
|-------|---------|-------------|
| **sprint-planner** | Sprint planning & task delegation | Every 1-2 weeks, major releases |
| **dotnet-setup** | Automate .NET SDK installation | New session setup |
| **tdd-helper** | Guide Red-Green-Refactor TDD workflow | ANY code changes (mandatory) |
| **precommit-check** | Validate before commits | Before EVERY commit (mandatory) |
| **test-coverage-analyzer** | Maintain 85%+ coverage target | After features, weekly audits |
| **race-condition-debugger** | Debug async/await issues | Intermittent test failures |
| **doc-sync-check** | Prevent stale documentation | Before commits, monthly audits |
| **docker-helper** | Docker security & optimization | Docker updates, monthly maintenance |

**Key Benefits:**
- ✅ Enforces mandatory TDD (Test-Driven Development)
- ✅ Prevents CI/CD failures with systematic pre-commit validation
- ✅ Maintains 85%+ test coverage requirement
- ✅ Prevents stale documentation through automated synchronization
- ✅ Ensures Docker security and optimization best practices

**Quick Usage:**
```bash
# Via slash commands (if configured)
/tdd-helper          # Start TDD workflow
/precommit-check     # Validate before commit
/doc-sync-check      # Check documentation sync

# Or follow step-by-step instructions in each skill file
cat .claude/skills/tdd-helper.md
```

See **[SKILLS.md](SKILLS.md)** for comprehensive documentation, decision trees, and complete workflow examples.

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
**Last Updated:** November 19, 2025
