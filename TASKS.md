# Project Tasks - Claude Code Test

**Last Updated:** 2025-11-18
**Repository:** scrawlsbenches/Claude-code-test

---

## Overview

This document consolidates all tasks across three major initiatives in the Claude Code Test repository. Tasks are organized by initiative, priority, and status.

**Quick Navigation:**
- [Core System Tasks](#1-core-system-tasks-hotswap-distributed-kernel) - Main orchestration system (95% complete)
- [Knowledge Graph Initiative](#2-knowledge-graph-initiative) - Graph storage and query system (Design complete, 0% implemented)
- [Build Server Initiative](#3-build-server-initiative) - Distributed build system (Design complete, 0% implemented)

---

## Task Summary

| Initiative | Total Tasks | Completed | In Progress | Not Started | Estimated Effort |
|-----------|-------------|-----------|-------------|-------------|------------------|
| **Core System** | 20 | 5 (25%) | 0 | 15 | 40-60 days |
| **Knowledge Graph** | 40 | 0 (0%) | 0 | 40 | 37.5 days (7.5 weeks) |
| **Build Server** | 30 | 0 (0%) | 0 | 30 | 153 hours (4 weeks) |
| **TOTAL** | **90** | **5 (6%)** | **0** | **85** | **~20 weeks** |

**Status Legend:**
- ✅ Completed
- ⏳ Not Started
- 🔄 In Progress
- ⚠️ Blocked

**Priority Legend:**
- 🔴 Critical - Required for production
- 🟡 High - Important for enterprise use
- 🟢 Medium - Valuable enhancements
- ⚪ Low - Nice-to-have features

---

# 1. Core System Tasks (HotSwap Distributed Kernel)

**Status:** Production Ready (95% Specification Compliance)
**Build Status:** ✅ Passing (582 tests: 568 passing, 14 skipped)
**Test Coverage:** 85%+

## 1.1 Completed Tasks (Sprint 1 - November 2025)

### ✅ Task 1: Authentication & Authorization
**Priority:** 🔴 Critical | **Status:** ✅ Completed (2025-11-15) | **Effort:** 1 day

**Implementation:**
- JWT bearer token authentication with configurable expiration
- Three user roles: Admin, Deployer, Viewer
- BCrypt password hashing for secure credentials
- Swagger UI with Bearer token auth
- 30+ comprehensive unit tests

**Files Created:**
- `src/HotSwap.Distributed.Domain/`: UserRole enum, User model, AuthenticationModels
- `src/HotSwap.Distributed.Infrastructure/`: IJwtTokenService, IUserRepository, JwtTokenService, InMemoryUserRepository
- `src/HotSwap.Distributed.Api/`: AuthenticationController
- `tests/`: JwtTokenServiceTests (15 tests), InMemoryUserRepositoryTests (15 tests)

**Documentation:** `JWT_AUTHENTICATION_GUIDE.md`

---

### ✅ Task 2: Approval Workflow System
**Priority:** 🔴 High | **Status:** ✅ Completed (2025-11-15) | **Effort:** 1 day

**Implementation:**
- Approval gates for Staging and Production deployments
- Email notifications to approvers (console logging)
- Approval timeout handling (24h auto-reject)
- Audit trail for approval decisions
- 10+ comprehensive unit tests

**API Endpoints:**
- `GET /api/v1/approvals/pending`
- `GET /api/v1/approvals/deployments/{executionId}`
- `POST /api/v1/approvals/deployments/{executionId}/approve`
- `POST /api/v1/approvals/deployments/{executionId}/reject`

**Documentation:** `APPROVAL_WORKFLOW_GUIDE.md`

---

### ✅ Task 3: PostgreSQL Audit Log Persistence
**Priority:** 🟡 Medium-High | **Status:** ✅ Complete (2025-11-16) | **Effort:** 2-3 days

**Implementation:**
- Comprehensive database schema: 5 tables with full indexing
- EF Core entity models and migrations
- AuditLogService with repository pattern
- Pipeline, approval, and authentication event logging
- Retention policy (90-day default, daily cleanup)
- 13 comprehensive unit tests

**Files Created:**
- `docs/AUDIT_LOG_SCHEMA.md`
- `src/HotSwap.Distributed.Infrastructure/Data/`: 5 entity models, AuditLogDbContext, migrations
- `src/HotSwap.Distributed.Infrastructure/Services/`: AuditLogService
- `src/HotSwap.Distributed.Api/Controllers/`: AuditLogsController (5 endpoints)

---

### ✅ Task 4: Integration Test Suite
**Priority:** 🟡 Medium | **Status:** ✅ Completed (2025-11-17) | **Effort:** 1 day

**Implementation:**
- 82 integration tests across 7 test files
- Testcontainers for PostgreSQL 16 and Redis 7
- Tests all deployment strategies, approval workflows, rollbacks, concurrency
- Tests messaging system and multi-tenant features
- CI/CD GitHub Actions job configured

**Test Coverage:**
- BasicIntegrationTests (9 tests): Auth, health checks, clusters
- DeploymentStrategyIntegrationTests (9 tests): All 4 strategies
- ApprovalWorkflowIntegrationTests (10 tests): Approval flows
- RollbackScenarioIntegrationTests (10 tests): Rollback scenarios
- ConcurrentDeploymentIntegrationTests (8 tests): Concurrency
- MessagingIntegrationTests (19 tests): Message lifecycle
- MultiTenantIntegrationTests (17 tests): Tenant management

**Documentation:** Updated `TESTING.md` with 327 lines

---

### ✅ Task 5: HTTPS/TLS Configuration
**Priority:** 🔴 Critical | **Status:** ✅ Completed (2025-11-15) | **Effort:** 1 day

**Implementation:**
- Kestrel configuration: HTTP (5000), HTTPS (5001)
- HSTS middleware (1-year max-age, subdomains)
- TLS 1.2+ enforcement
- Development certificate generation script
- Docker Compose HTTPS support

**Files Created:**
- `generate-dev-cert.sh`
- Updated `appsettings.json`, `Program.cs`, `docker-compose.yml`
- `HTTPS_SETUP_GUIDE.md`

---

## 1.2 High Priority Tasks (Not Started)

### Task 6: WebSocket Real-Time Updates
**Priority:** 🟢 Medium | **Status:** ⏳ Not Started | **Effort:** 2-3 days

**Requirements:**
- [ ] Add SignalR NuGet package
- [ ] Create DeploymentHub for WebSocket connections
- [ ] Real-time deployment status updates
- [ ] Real-time metrics streaming
- [ ] Client subscription management
- [ ] JavaScript client example

**Features:**
- Subscribe to deployment progress
- Live pipeline stage updates
- Cluster health change notifications

**References:** BUILD_STATUS.md:378, PROJECT_STATUS_REPORT.md:519

---

### Task 7: Prometheus Metrics Exporter
**Priority:** 🟢 Medium | **Status:** ⏳ Not Started | **Effort:** 1-2 days

**Requirements:**
- [ ] Add Prometheus.AspNetCore.HealthChecks package
- [ ] Configure Prometheus exporter endpoint (`/metrics`)
- [ ] Export all OpenTelemetry metrics
- [ ] Add custom business metrics
- [ ] Create Grafana dashboard JSON
- [ ] Document Prometheus setup

**References:** SPEC_COMPLIANCE_REVIEW.md:343, BUILD_STATUS.md:381

---

### Task 8: Helm Charts for Kubernetes
**Priority:** 🟢 Medium | **Status:** ⏳ Not Started | **Effort:** 2 days

**Requirements:**
- [ ] Create Helm chart structure
- [ ] Define deployment templates
- [ ] Create ConfigMap and Secret templates
- [ ] Add Service and Ingress templates
- [ ] Configure HPA (Horizontal Pod Autoscaler)
- [ ] Add PodDisruptionBudget
- [ ] Create values.yaml with defaults
- [ ] Test on Kubernetes 1.26, 1.27, 1.28

**References:** BUILD_STATUS.md:387, PROJECT_STATUS_REPORT.md:521

---

### Task 9: Service Discovery Integration
**Priority:** 🟢 Low-Medium | **Status:** ⏳ Not Started | **Effort:** 2-3 days

**Requirements:**
- [ ] Add Consul client NuGet package
- [ ] Implement IServiceDiscovery interface
- [ ] Create ConsulServiceDiscovery implementation
- [ ] Automatic node registration
- [ ] Health check registration
- [ ] Service lookup and caching
- [ ] Support multiple backends (Consul, etcd)

**References:** SPEC_COMPLIANCE_REVIEW.md:242, PROJECT_STATUS_REPORT.md:502

---

### Task 10: Load Testing Suite
**Priority:** 🟢 Low-Medium | **Status:** ⏳ Not Started | **Effort:** 2 days

**Requirements:**
- [ ] Create k6 load test scripts
- [ ] Test deployment endpoint under load
- [ ] Test metrics endpoint under load
- [ ] Test concurrent deployments
- [ ] Measure API latency percentiles (p50, p95, p99)
- [ ] Identify performance bottlenecks
- [ ] Document performance characteristics

**Test Scenarios:**
- Sustained load: 100 req/s for 10 minutes
- Spike test: 0 → 500 req/s
- Soak test: 50 req/s for 1 hour
- Stress test: Increase until breaking point

**References:** TESTING.md:236

---

## 1.3 Low Priority Tasks

### Task 11: GraphQL API Layer
**Priority:** ⚪ Low | **Status:** ⏳ Not Started | **Effort:** 3-4 days

**Requirements:**
- [ ] Add HotChocolate NuGet package
- [ ] Create GraphQL schema
- [ ] Implement queries (deployments, clusters, metrics)
- [ ] Implement mutations (create deployment, rollback)
- [ ] Implement subscriptions (real-time updates)
- [ ] Add GraphQL playground
- [ ] Create client examples

**References:** BUILD_STATUS.md:390

---

### Task 12: Multi-Tenancy Support
**Priority:** ⚪ Low | **Status:** ⏳ Not Started | **Effort:** 4-5 days

**Note:** Multi-tenant system already implemented for website hosting. This task is for extending kernel orchestration to support multi-tenancy.

**Requirements:**
- [ ] Add tenant context to all operations
- [ ] Implement tenant isolation
- [ ] Create tenant management API
- [ ] Add tenant-specific configurations
- [ ] Implement tenant-based metrics
- [ ] Add tenant-based audit logs

**References:** BUILD_STATUS.md:391, docs/MULTITENANT_IMPLEMENTATION_SUMMARY.md

---

### Task 13: ML-Based Anomaly Detection
**Priority:** ⚪ Low | **Status:** ⏳ Not Started | **Effort:** 5-7 days

**Requirements:**
- [ ] Integrate ML.NET or Azure ML
- [ ] Collect historical metrics data
- [ ] Train anomaly detection model
- [ ] Implement real-time anomaly detection
- [ ] Add anomaly alerting
- [ ] Create anomaly dashboard

**References:** BUILD_STATUS.md:392

---

### Task 14: Admin Dashboard UI
**Priority:** ⚪ Low | **Status:** ⏳ Not Started | **Effort:** 7-10 days

**Requirements:**
- [ ] Create React/Vue.js frontend
- [ ] Implement deployment dashboard
- [ ] Add cluster monitoring views
- [ ] Create metrics visualization
- [ ] Add deployment history
- [ ] Implement user management UI
- [ ] Add approval workflow UI

**References:** BUILD_STATUS.md:393

---

## 1.4 Security Tasks

### Task 15: API Rate Limiting (Already Implemented)
**Priority:** 🟡 Medium | **Status:** ✅ Completed (2025-11-15) | **Effort:** 1 day

**Implementation:**
- RateLimitingMiddleware with sliding window algorithm
- IP-based and token-based rate limiting
- Configurable per-endpoint limits
- HTTP 429 responses with Retry-After headers
- 10 comprehensive unit tests

**Rate Limits (Production):**
- Global: 1000 req/min per IP
- Deployments: 10 req/min per user
- Clusters: 60 req/min per user
- Approvals: 30 req/min per user
- Auth: 5 req/min per user

---

### Task 16: Secret Rotation System
**Priority:** 🟡 High | **Status:** ⏳ Not Started | **Effort:** 2-3 days

**Requirements:**
- [ ] Integrate Azure Key Vault or HashiCorp Vault
- [ ] Implement automatic secret rotation
- [ ] Add secret versioning
- [ ] Configure rotation policies
- [ ] Add secret expiration monitoring
- [ ] Create runbook for manual rotation

**References:** README.md:241, PROJECT_STATUS_REPORT.md:674

---

### Task 17: OWASP Top 10 Security Review
**Priority:** 🟡 High | **Status:** ⏳ Not Started | **Effort:** 2-3 days

**Requirements:**
- [ ] A01:2021 - Broken Access Control (✅ RBAC implemented)
- [ ] A02:2021 - Cryptographic Failures (Review signatures)
- [ ] A03:2021 - Injection (Review input validation)
- [ ] A04:2021 - Insecure Design (Architecture review)
- [ ] A05:2021 - Security Misconfiguration (Review configs)
- [ ] A06:2021 - Vulnerable Components (Update dependencies)
- [ ] A07:2021 - Authentication Failures (✅ JWT implemented)
- [ ] A08:2021 - Software/Data Integrity (✅ Implemented)
- [ ] A09:2021 - Security Logging Failures (Review logs)
- [ ] A10:2021 - SSRF (Review HTTP clients)

**References:** README.md:242, PROJECT_STATUS_REPORT.md:678

---

## 1.5 Documentation Tasks

### Task 18: API Client SDKs
**Priority:** 🟢 Medium | **Status:** ⏳ Not Started | **Effort:** 3-4 days per language

**Requirements:**
- [ ] Create TypeScript/JavaScript SDK
- [ ] Create Python SDK
- [ ] Create Java SDK
- [ ] Create Go SDK
- [ ] Publish to package managers (npm, PyPI, Maven)
- [ ] Add SDK documentation
- [ ] Create SDK examples

**Note:** C# example exists in `examples/ApiUsageExample/`

---

### Task 19: Architecture Decision Records (ADR)
**Priority:** 🟢 Low | **Status:** ⏳ Not Started | **Effort:** 2 days

**Requirements:**
- [ ] Document deployment strategy decisions
- [ ] Document technology choices
- [ ] Document security architecture
- [ ] Document scalability decisions
- [ ] Create ADR template
- [ ] Store in `docs/adr/`

---

### Task 20: Runbooks and Operations Guide
**Priority:** 🟢 Medium | **Status:** ⏳ Not Started | **Effort:** 2-3 days

**Requirements:**
- [ ] Create incident response runbook
- [ ] Document rollback procedures
- [ ] Create troubleshooting guide
- [ ] Document monitoring setup
- [ ] Add alerting configuration guide
- [ ] Create disaster recovery plan
- [ ] Document backup/restore procedures

---

# 2. Knowledge Graph Initiative

**Status:** Design Complete, Not Started (0% implemented)
**Estimated Effort:** 37.5 days (7.5 weeks)
**Total Tasks:** 40 tasks across 8 epics

**Design Document:** `docs/KNOWLEDGE_GRAPH_DESIGN.md`

## 2.1 Overview

This initiative builds a knowledge graph system on top of the HotSwap Distributed Kernel, adding graph storage, query execution, and zero-downtime schema evolution capabilities.

**Key Features:**
- PostgreSQL-backed graph storage with JSONB
- Pattern matching and graph traversal (BFS, DFS, shortest path)
- Schema versioning with backward compatibility
- REST API with 25+ endpoints
- Hot-swappable query algorithms
- Zero-downtime schema migrations

**Technology Stack:**
- PostgreSQL with JSONB columns for properties
- Entity Framework Core for ORM
- Redis for query result caching
- OpenTelemetry for distributed tracing

---

## 2.2 Phase 1: Core Foundation (10 days)

### Epic 1: Core Graph Domain Model (5 tasks, 4 days)

**Task 2.1:** Entity Model Implementation
- **Priority:** 🔴 Critical | **Status:** ⏳ Not Started | **Effort:** 1 day
- Implement `Entity` class with Id, Type, Properties, timestamps
- 10 unit tests

**Task 2.2:** Relationship Model Implementation
- **Priority:** 🔴 Critical | **Status:** ⏳ Not Started | **Effort:** 1 day
- Implement `Relationship` class with Source, Target, Weight, IsDirected
- 10 unit tests

**Task 2.3:** Graph Schema Model Implementation
- **Priority:** 🔴 Critical | **Status:** ⏳ Not Started | **Effort:** 1 day
- Schema versioning and backward compatibility checking
- 15 unit tests

**Task 2.4:** Graph Query Model Implementation
- **Priority:** 🔴 Critical | **Status:** ⏳ Not Started | **Effort:** 0.5 day
- Pattern matching syntax, pagination support
- 8 unit tests

**Task 2.5:** Domain Enums and Value Objects
- **Priority:** 🔴 Critical | **Status:** ⏳ Not Started | **Effort:** 0.5 day
- Direction, IndexType, PropertyType, QueryOperator enums
- 5 unit tests

---

### Epic 2: PostgreSQL Graph Storage (6 tasks, 6 days)

**Task 2.6:** Entity Framework Core Models
- **Priority:** 🔴 Critical | **Status:** ⏳ Not Started | **Effort:** 1 day
- EF Core entities: EntityRecord, RelationshipRecord, SchemaVersionRecord
- JSONB columns for properties, GIN indexes

**Task 2.7:** Graph DbContext Implementation
- **Priority:** 🔴 Critical | **Status:** ⏳ Not Started | **Effort:** 0.5 day
- PostgreSQL-specific configuration, cascade deletes

**Task 2.8:** Database Migrations
- **Priority:** 🔴 Critical | **Status:** ⏳ Not Started | **Effort:** 0.5 day
- Initial migration with GIN indexes

**Task 2.9:** Graph Repository Interface
- **Priority:** 🔴 Critical | **Status:** ⏳ Not Started | **Effort:** 0.5 day
- Entity/relationship CRUD methods, batch operations

**Task 2.10:** PostgreSQL Repository Implementation
- **Priority:** 🔴 Critical | **Status:** ⏳ Not Started | **Effort:** 2 days
- Efficient JSONB queries, transaction support
- 20 integration tests

**Task 2.11:** Graph Indexing Service
- **Priority:** 🟢 Medium | **Status:** ⏳ Not Started | **Effort:** 1 day
- Dynamic index creation, full-text search support
- 10 unit tests

---

## 2.3 Phase 2: Query Engine (10 days)

### Epic 3: Graph Query Engine (8 tasks)

**Task 2.12-2.19:** Pattern matching, graph traversal (BFS, DFS, shortest path), query optimization, query service
- 8 tasks, 7.5 days total
- Includes pattern matcher (2 days), traversal algorithms (2.5 days), optimizer (1 day), query service (1 day)

**Detailed task breakdown available in:** `docs/KNOWLEDGE_GRAPH_DESIGN.md`

---

## 2.4 Phase 3: REST API (7.5 days)

### Epic 4: Knowledge Graph REST API (6 tasks)

**Task 2.20-2.25:** Entities, Relationships, Queries, Schema, Visualization controllers, API startup config
- 6 tasks, 7.5 days total
- 25+ REST API endpoints
- Authentication and authorization integration
- OpenAPI documentation

---

## 2.5 Phase 4: HotSwap Integration (5 days)

### Epic 5: HotSwap Integration (5 tasks)

**Task 2.26-2.30:** Graph module descriptor, schema migration strategy, query algorithm hot-swap, partition management, integration testing
- 5 tasks, 5 days total
- Zero-downtime schema migrations
- A/B testing for query algorithms
- Automatic rollback on regression

---

## 2.6 Phase 5: Optimization & Polish (5 days)

### Epic 6: Comprehensive Testing (3 tasks)
### Epic 7: Performance Optimization (3 tasks)
### Epic 8: Documentation & Examples (4 tasks)

**Tasks 2.31-2.40:** Unit tests (100+ tests), integration tests, E2E tests, query caching, database optimization, batch processing, API docs, developer guide, examples
- 10 tasks, 5 days total

---

## 2.7 Success Criteria

**MVP (End of Phase 3):**
- 80+ unit tests passing (90%+ coverage)
- All CRUD operations working
- Pattern matching and graph traversal implemented
- REST API with 25+ endpoints
- Sub-100ms simple query latency

**Production-Ready (End of Phase 5):**
- All MVP criteria met
- HotSwap integration complete
- 130+ comprehensive tests
- Query result caching (>80% hit rate)
- Zero-downtime schema migration demonstrated

**Full task breakdown:** See `docs/KNOWLEDGE_GRAPH_DESIGN.md`

---

# 3. Build Server Initiative

**Status:** Design Complete, Not Started (0% implemented)
**Estimated Effort:** 153 hours (4 weeks)
**Total Tasks:** 30 tasks across 5 phases

**Design Document:** `docs/BUILD_SERVER_DESIGN.md`

## 3.1 Overview

This initiative implements a distributed .NET build server using the HotSwap.Distributed framework for orchestration, adding parallel builds, caching, and canary builds.

**Key Features:**
- Distributed builds across multiple agents
- Build strategies: Incremental, Clean, Cached, Distributed, Canary
- Git repository integration
- NuGet package management
- Artifact storage and signing
- Real-time build progress tracking

**Technology Stack:**
- Reuses HotSwap infrastructure (telemetry, metrics)
- Redis for build caching
- PostgreSQL for build history
- Docker for agent isolation

---

## 3.2 Phase 1: Foundation (Week 1, 38 hours)

**Tasks 3.1-3.8:**
- Project structure setup
- Domain models (BuildJobDescriptor, BuildRequest, BuildResult)
- Build agent capabilities detection
- Agent pool infrastructure
- Unit tests for domain models

**Key Deliverables:**
- Complete project structure
- All domain models
- Agent capability detection
- 30+ unit tests

---

## 3.3 Phase 2: Core Build Logic (Week 2, 32 hours)

**Tasks 3.9-3.14:**
- Build agent core structure
- Git integration (clone, authentication)
- NuGet restore
- dotnet build integration
- dotnet test integration
- Artifact packaging

**Key Deliverables:**
- Fully functional build agent
- Git repository cloning
- Full .NET build pipeline
- Test execution
- Artifact creation

---

## 3.4 Phase 3: Orchestration (Week 3, 42 hours)

**Tasks 3.15-3.21:**
- Build strategy interface
- Incremental build strategy
- Clean build strategy
- Cached build strategy (Redis)
- Build pipeline
- Build orchestrator
- Unit tests for strategies

**Key Deliverables:**
- 4 build strategies implemented
- Build pipeline with stages
- Complete orchestrator
- 40+ unit tests

---

## 3.5 Phase 4: API Layer (Week 4, 20 hours)

**Tasks 3.22-3.26:**
- API models and validation
- BuildsController (create, status, list, cancel, logs)
- AgentsController (list, health, maintenance)
- API startup configuration
- Integration tests

**Key Deliverables:**
- REST API with 10+ endpoints
- OpenAPI/Swagger documentation
- Authentication integration
- 20+ integration tests

---

## 3.6 Phase 5: Advanced Features (Weeks 5-6, 21 hours)

**Tasks 3.27-3.30:**
- Distributed build strategy (parallel builds)
- Canary build strategy (safe production builds)
- Build artifact storage (Azure Blob/S3)
- Docker Compose deployment

**Key Deliverables:**
- Parallel distributed builds
- Production-safe canary builds
- Artifact storage and retrieval
- Full Docker stack

---

## 3.7 Future Enhancements

**Tasks 3.31-3.36 (Not in initial 4-week scope):**
- Authentication & Authorization (16 hours)
- Build Approval Workflow (12 hours)
- Secret Management (8 hours)
- Prometheus Metrics Export (8 hours)
- WebSocket Real-Time Updates (12 hours)
- Build History Analytics (16 hours)

---

## 3.8 Success Criteria

**Week 2 Checkpoint:**
- Build agent can clone repos and build .NET projects
- Basic orchestration working

**Week 4 Checkpoint:**
- REST API functional
- All 4 basic strategies working
- Integration tests passing

**Week 6 Checkpoint:**
- Distributed and canary strategies working
- Production-ready deployment
- Full documentation

**Full task breakdown:** See `docs/BUILD_SERVER_DESIGN.md` and original task list

---

# 4. Task Dependencies

## 4.1 Core System Dependencies

```
Authentication (Task 1) → RBAC, Approval Workflow (Task 2)
PostgreSQL Setup → Audit Logs (Task 3)
Integration Tests (Task 4) → CI/CD validation
```

## 4.2 Knowledge Graph Dependencies

```
Phase 1 (Foundation) → Phase 2 (Query Engine) → Phase 3 (API) → Phase 4 (HotSwap) → Phase 5 (Polish)

Critical Path: Entity → Relationship → Schema → Repository → Query Service → API → Integration
```

## 4.3 Build Server Dependencies

```
Phase 1 (Foundation) → Phase 2 (Build Logic) → Phase 3 (Orchestration) → Phase 4 (API) → Phase 5 (Advanced)

Critical Path: Project Setup → Agent → Git → Build → Orchestrator → API
```

---

# 5. Getting Started

## 5.1 For Core System Tasks
1. Review `PROJECT_STATUS_REPORT.md` for current status
2. Check `SPEC_COMPLIANCE_REVIEW.md` for compliance gaps
3. Follow TDD workflow from `CLAUDE.md`
4. Run pre-commit checklist before committing

## 5.2 For Knowledge Graph Initiative
1. Read `docs/KNOWLEDGE_GRAPH_DESIGN.md` for architecture
2. Start with Phase 1, Task 2.1 (Entity Model)
3. Follow dependency graph
4. Create GitHub issues for each task

## 5.3 For Build Server Initiative
1. Read `docs/BUILD_SERVER_DESIGN.md` for architecture
2. Start with Phase 1, Task 3.1 (Project Structure)
3. Follow weekly milestones
4. Reuse HotSwap infrastructure components

---

# 6. References

**Core Documentation:**
- [CLAUDE.md](CLAUDE.md) - AI assistant guide, development workflows
- [README.md](README.md) - Project overview and quick start
- [Documentation Index](docs/README.md) - Complete documentation guide

**Status & Compliance:**
- [PROJECT_STATUS_REPORT.md](docs/PROJECT_STATUS_REPORT.md) - Current project status
- [SPEC_COMPLIANCE_REVIEW.md](docs/SPEC_COMPLIANCE_REVIEW.md) - Specification compliance
- [BUILD_STATUS.md](docs/BUILD_STATUS.md) - Build validation report
- [TESTING.md](docs/TESTING.md) - Testing strategy and results

**Design Documents:**
- [Knowledge Graph Design](docs/KNOWLEDGE_GRAPH_DESIGN.md)
- [Build Server Design](docs/BUILD_SERVER_DESIGN.md)
- [Multi-Tenant System Plan](docs/MULTITENANT_WEBSITE_SYSTEM_PLAN.md)

**Implementation Guides:**
- [JWT Authentication Guide](docs/JWT_AUTHENTICATION_GUIDE.md)
- [HTTPS Setup Guide](docs/HTTPS_SETUP_GUIDE.md)
- [Approval Workflow Guide](docs/APPROVAL_WORKFLOW_GUIDE.md)
- [Autonomous Agent Onboarding](docs/AUTONOMOUS_AGENT_ONBOARDING.md)

---

**Maintained By:** Development Team
**Review Frequency:** Weekly
**Last Updated:** 2025-11-18
**Next Review:** Before Sprint 2 kickoff
