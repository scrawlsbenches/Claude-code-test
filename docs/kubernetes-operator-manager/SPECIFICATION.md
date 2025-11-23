# Kubernetes Operator Manager - Technical Specification

**Version:** 1.0.0
**Date:** 2025-11-23
**Status:** Design Specification
**Authors:** Platform Architecture Team

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [System Requirements](#system-requirements)
3. [Deployment Strategies](#deployment-strategies)
4. [Performance Requirements](#performance-requirements)
5. [Security Requirements](#security-requirements)
6. [Observability Requirements](#observability-requirements)
7. [Scalability Requirements](#scalability-requirements)

---

## Executive Summary

The Kubernetes Operator Manager provides enterprise-grade operator lifecycle management built on the existing kernel orchestration platform. The system manages operator deployments across multiple Kubernetes clusters with progressive rollout strategies, automated health validation, and zero-downtime upgrades.

### Key Innovations

1. **Multi-Cluster Orchestration** - Centralized operator management across environments
2. **Progressive Deployment Strategies** - Canary, Blue-Green, Rolling, Direct deployments
3. **CRD Compatibility Validation** - Schema evolution tracking and breaking change detection
4. **Automated Rollback** - Health-based automatic rollback to previous operator versions
5. **Zero-Downtime Upgrades** - Operator updates without custom resource disruption

### Design Principles

1. **Leverage Existing Infrastructure** - Reuse authentication, tracing, metrics, approval workflows
2. **Clean Architecture** - Maintain 4-layer separation (API, Orchestrator, Infrastructure, Domain)
3. **Test-Driven Development** - 85%+ test coverage with comprehensive unit/integration tests
4. **Production-Ready** - Security, observability, and reliability from day one
5. **Kubernetes-Native** - Use kubectl, Helm, and Kubernetes APIs

---

## System Requirements

### Functional Requirements

#### FR-OP-001: Operator Registration
**Priority:** Critical
**Description:** System MUST support registering operators with metadata and versioning

**Requirements:**
- Register operator with name, namespace, Helm chart details
- Track operator versions (semantic versioning)
- Store CRD schemas associated with each operator version
- Validate Helm chart repository accessibility
- Generate unique operator ID
- Return operator metadata

**API Endpoint:**
```
POST /api/v1/operators
```

**Acceptance Criteria:**
- Operator ID generated (GUID format)
- Helm repository validated before registration
- Invalid operators rejected with 400 Bad Request
- Operator metadata persisted to PostgreSQL
- Audit log entry created

---

#### FR-OP-002: Cluster Registration
**Priority:** Critical
**Description:** System MUST support registering Kubernetes clusters with kubeconfig

**Requirements:**
- Register cluster with name, kubeconfig, environment
- Encrypt kubeconfig credentials at rest
- Validate cluster connectivity
- Detect Kubernetes version
- Track cluster health status
- Support multiple environments (Dev, Staging, Production)

**API Endpoints:**
```
POST /api/v1/clusters
GET  /api/v1/clusters
GET  /api/v1/clusters/{name}
GET  /api/v1/clusters/{name}/health
```

**Acceptance Criteria:**
- Kubeconfig encrypted using AES-256
- Cluster connectivity validated before registration
- Kubernetes version detected automatically
- Cluster health checked every 60 seconds
- Unreachable clusters marked as Unhealthy

---

#### FR-OP-003: Operator Deployment
**Priority:** Critical
**Description:** System MUST support deploying operators to clusters with strategy selection

**Requirements:**
- Deploy operator to one or more clusters
- Select deployment strategy (Canary, Blue-Green, Rolling, Direct)
- Validate CRD compatibility before deployment
- Track deployment progress
- Support environment-specific configurations
- Enable/disable deployment approval workflow

**Deployment Lifecycle:**
1. **Planning** - Validate clusters, CRDs, configurations
2. **Deploying** - Execute deployment strategy
3. **Validating** - Health checks on operator controllers
4. **Completed** - All clusters successfully upgraded
5. **RollingBack** - Automatic rollback on failures
6. **Failed** - Manual intervention required

**API Endpoints:**
```
POST   /api/v1/deployments
GET    /api/v1/deployments
GET    /api/v1/deployments/{id}
POST   /api/v1/deployments/{id}/rollback
DELETE /api/v1/deployments/{id}
```

**Acceptance Criteria:**
- Deployment ID generated and tracked
- CRD compatibility validated before deployment
- Deployment status updated in real-time
- Failed deployments automatically rolled back
- Deployment history preserved

---

#### FR-OP-004: CRD Management
**Priority:** High
**Description:** System MUST track CRD schemas and validate compatibility

**Requirements:**
- Store CRD schemas per operator version
- Detect schema changes (added fields, removed fields, type changes)
- Identify breaking changes
- Require approval for breaking CRD changes (production)
- Support CRD version migration guides
- Track CRD status across clusters

**Breaking Changes:**
- Required field added
- Existing field removed
- Field type changed
- Validation constraints tightened

**API Endpoints:**
```
POST /api/v1/crds
GET  /api/v1/crds
GET  /api/v1/crds/{name}
POST /api/v1/crds/{name}/validate
POST /api/v1/crds/{name}/approve (admin only)
```

**Acceptance Criteria:**
- CRD schemas stored with version history
- Breaking changes detected automatically
- Production CRD updates require approval
- CRD validation time < 5s (p99)

---

#### FR-OP-005: Operator Health Monitoring
**Priority:** Critical
**Description:** System MUST monitor operator health and trigger rollback on failures

**Requirements:**
- Monitor operator controller pod status
- Check webhook endpoint availability (if applicable)
- Validate CRD instances are being reconciled
- Track operator error rates and logs
- Detect controller crash loops
- Automatic rollback on sustained failures

**Health Check Types:**

1. **Pod Health**
   - Controller pods in Running state
   - No crash loops (< 3 restarts in 5 minutes)
   - Resource usage within limits

2. **Webhook Health**
   - Webhook endpoint responds with 200 OK
   - Webhook latency < 1s
   - Certificate validity checked

3. **CRD Reconciliation Health**
   - Custom resources being reconciled
   - No stale resources (lastTransitionTime < 5m)
   - Error rate < 5%

**API Endpoints:**
```
GET /api/v1/operators/{name}/health
GET /api/v1/clusters/{cluster}/operators/{name}/health
```

**Acceptance Criteria:**
- Health checks run every 30 seconds
- Unhealthy operators trigger alerts
- 3 consecutive failed health checks trigger rollback
- Health metrics exposed via Prometheus

---

#### FR-OP-006: Rollback Mechanism
**Priority:** Critical
**Description:** System MUST support automatic and manual rollback to previous operator versions

**Requirements:**
- Store previous operator version metadata
- Preserve previous CRD schemas
- Automatic rollback on health check failures
- Manual rollback via API
- Rollback status tracking
- State preservation (existing custom resources)

**Rollback Triggers:**
- Controller pod crash loops
- Webhook endpoint failures
- CRD reconciliation failures
- Manual admin request

**Rollback Process:**
1. Pause new deployments
2. Restore previous operator version
3. Validate CRD schema compatibility
4. Deploy previous Helm chart
5. Validate health checks
6. Resume normal operations

**API Endpoints:**
```
POST /api/v1/deployments/{id}/rollback
GET  /api/v1/deployments/{id}/rollback-status
```

**Acceptance Criteria:**
- Rollback completes within 3 minutes
- Custom resources preserved during rollback
- Zero custom resource data loss
- Rollback success rate > 99%

---

#### FR-OP-007: Approval Workflow
**Priority:** High
**Description:** System MUST support approval workflow for production operator deployments

**Requirements:**
- Require approval for production operator upgrades
- Require approval for breaking CRD changes
- Support multi-level approval (optional)
- Email notifications for pending approvals
- Approval audit trail

**Approval Types:**
- **Operator Version Upgrade** - Requires admin approval
- **Breaking CRD Change** - Requires admin approval
- **Production Deployment** - Requires environment owner approval

**API Endpoints:**
```
POST /api/v1/approvals/{id}/approve (admin only)
POST /api/v1/approvals/{id}/reject (admin only)
GET  /api/v1/approvals (list pending)
```

**Acceptance Criteria:**
- Production deployments blocked until approved
- Email notifications sent within 30 seconds
- Approval history preserved
- Rejected deployments not executed

---

## Deployment Strategies

### 1. Canary Deployment

**Use Case:** Progressive rollout with gradual traffic shift

**Behavior:**
- Deploy new operator version to small percentage of clusters
- Validate health metrics
- Gradually increase percentage
- Rollback on failures

**Configuration:**
```json
{
  "strategy": "Canary",
  "canaryConfig": {
    "initialPercentage": 10,
    "incrementPercentage": 20,
    "evaluationPeriod": "PT5M",
    "successThreshold": 0.95
  }
}
```

**Deployment Flow:**
```
Stage 1: 10% of clusters (Dev)
  ├─ Deploy new operator version
  ├─ Wait 5 minutes
  ├─ Validate health (95%+ success rate)
  └─ Continue OR Rollback

Stage 2: 30% of clusters (Dev + Staging)
  ├─ Deploy to additional 20%
  ├─ Wait 5 minutes
  ├─ Validate health
  └─ Continue OR Rollback

Stage 3: 100% of clusters (All environments)
  ├─ Deploy to remaining 70%
  ├─ Validate health
  └─ Complete OR Rollback
```

**Rollback Conditions:**
- Health check success rate < 95%
- Controller pod crash loops
- Webhook failures

---

### 2. Blue-Green Deployment

**Use Case:** Zero-downtime deployment with instant rollback

**Behavior:**
- Deploy new operator version to "green" namespace
- Validate health in green environment
- Switch traffic from blue to green
- Keep blue as rollback target

**Configuration:**
```json
{
  "strategy": "BlueGreen",
  "blueGreenConfig": {
    "blueNamespace": "operators-blue",
    "greenNamespace": "operators-green",
    "validationPeriod": "PT10M",
    "autoSwitch": true
  }
}
```

**Deployment Flow:**
```
1. Deploy to Green Namespace
   ├─ Install operator v2.0 in operators-green
   ├─ Validate CRDs, webhooks, controllers
   └─ Wait validation period (10 minutes)

2. Switch Traffic (if validation passes)
   ├─ Update CRD webhooks to green
   ├─ Scale down blue controllers to 0
   └─ Monitor for 5 minutes

3. Cleanup (if successful)
   ├─ Keep blue namespace for 24 hours
   └─ Delete blue deployment after retention period
```

**Rollback:**
- Instant switch back to blue namespace
- No redeployment required

---

### 3. Rolling Deployment

**Use Case:** Cluster-by-cluster deployment with validation gates

**Behavior:**
- Deploy to one cluster at a time
- Validate health before proceeding to next cluster
- Environment-based ordering (Dev → Staging → Prod)

**Configuration:**
```json
{
  "strategy": "Rolling",
  "rollingConfig": {
    "clusterOrder": ["dev-1", "dev-2", "staging", "prod-us", "prod-eu"],
    "validationPeriod": "PT5M",
    "pauseBetweenClusters": "PT2M"
  }
}
```

**Deployment Flow:**
```
Cluster 1 (dev-1)
  ├─ Deploy operator v2.0
  ├─ Wait 5 minutes
  ├─ Validate health
  └─ Continue OR Rollback

Cluster 2 (dev-2)
  ├─ Wait 2 minutes (pause)
  ├─ Deploy operator v2.0
  ├─ Wait 5 minutes
  └─ Continue OR Rollback

... (repeat for remaining clusters)
```

---

### 4. Direct Deployment

**Use Case:** Immediate deployment to all clusters (non-production)

**Behavior:**
- Deploy to all clusters simultaneously
- Minimal validation
- Fastest deployment time

**Configuration:**
```json
{
  "strategy": "Direct",
  "directConfig": {
    "parallelClusters": 10,
    "skipHealthChecks": false
  }
}
```

**Deployment Flow:**
```
All Clusters (parallel)
  ├─ Deploy to all clusters concurrently
  ├─ Wait for Helm install to complete
  ├─ Optional health check
  └─ Complete
```

**Warning:** Not recommended for production environments

---

## Performance Requirements

### Deployment Performance

| Metric | Target | Notes |
|--------|--------|-------|
| Single cluster deployment | < 3 min | Helm install + health check |
| Multi-cluster deployment (10 clusters) | < 15 min | Parallel deployments |
| Canary deployment (3 stages) | < 30 min | Including evaluation periods |
| Rollback time | < 3 min | To previous version |

### Health Check Performance

| Operation | p50 | p95 | p99 |
|-----------|-----|-----|-----|
| Pod health check | 100ms | 300ms | 500ms |
| Webhook health check | 200ms | 500ms | 1s |
| CRD reconciliation check | 500ms | 1s | 2s |
| Full operator health | 1s | 2s | 3s |

### API Performance

| Endpoint | Target Latency | Notes |
|----------|----------------|-------|
| GET /api/v1/operators | < 100ms | List operators |
| POST /api/v1/deployments | < 500ms | Start deployment |
| GET /api/v1/clusters/{name}/health | < 2s | Full cluster health |

### Scalability Targets

| Resource | Limit | Notes |
|----------|-------|-------|
| Max Operators | 500 | Per system |
| Max Clusters | 100 | Per system |
| Max Concurrent Deployments | 20 | System-wide |
| Max CRDs per Operator | 50 | Tracked CRDs |

---

## Security Requirements

### Authentication

**Requirement:** All API endpoints MUST require JWT authentication (except /health)

**Implementation:**
- Reuse existing JWT authentication middleware
- Validate token on every request
- Extract user identity and roles

### Authorization

**Role-Based Access Control:**

| Role | Permissions |
|------|-------------|
| **Admin** | Full access (approve deployments, manage clusters, rollback) |
| **Operator** | Deploy operators, view status, manage operators |
| **Viewer** | Read-only access (view operators, deployments, clusters) |
| **Environment Owner** | Approve deployments to specific environment |

**Endpoint Authorization:**
```
POST   /api/v1/operators           - Operator, Admin
POST   /api/v1/deployments         - Operator, Admin
POST   /api/v1/deployments/{id}/rollback - Admin only
POST   /api/v1/approvals/{id}/approve - Admin, Environment Owner
DELETE /api/v1/clusters/{name}    - Admin only
```

### Cluster Credential Security

**Requirements:**
- Kubeconfig files encrypted at rest (AES-256)
- Kubeconfig never returned in API responses
- Kubeconfig stored in PostgreSQL with encryption
- Cluster access via temporary service accounts
- Audit all cluster access

### Network Security

**Requirements:**
- HTTPS/TLS 1.2+ enforced (production)
- mTLS for cluster-to-cluster communication (optional)
- Network policies for Kubernetes clusters
- Firewall rules for cluster API access

### Rate Limiting

**Limits (Production):**
```
POST /api/v1/deployments:  10 req/min per user
GET  /api/v1/operators:    60 req/min per user
GET  /api/v1/clusters:     60 req/min per user
POST /api/v1/clusters:     5 req/min per user
```

---

## Observability Requirements

### Distributed Tracing

**Requirement:** ALL operator operations MUST be traced end-to-end

**Spans:**
1. `operator.deploy` - Deployment operation
2. `operator.validate` - CRD validation
3. `cluster.connect` - Cluster connectivity check
4. `helm.install` - Helm chart installation
5. `operator.healthcheck` - Health validation
6. `operator.rollback` - Rollback operation

**Trace Context:**
- Propagate W3C trace context to Kubernetes operations
- Link deployment and health check spans
- Include operator metadata in span attributes

**Example Trace:**
```
Root Span: operator.deploy
  ├─ Child: operator.validate (CRD compatibility)
  ├─ Child: cluster.connect (target clusters)
  ├─ Child: helm.install (per cluster)
  │   ├─ Child: crd.apply
  │   └─ Child: controller.deploy
  └─ Child: operator.healthcheck
      ├─ Child: pod.healthcheck
      ├─ Child: webhook.healthcheck
      └─ Child: crd.reconciliation.check
```

### Metrics

**Required Metrics:**

**Counters:**
- `operators.deployed.total` - Total operator deployments
- `operators.rollback.total` - Total rollbacks
- `operators.failed.total` - Total failed deployments
- `clusters.registered.total` - Total clusters registered
- `crds.validated.total` - Total CRD validations

**Histograms:**
- `operator.deploy.duration` - Deployment duration
- `operator.rollback.duration` - Rollback duration
- `operator.healthcheck.duration` - Health check latency
- `helm.install.duration` - Helm install time

**Gauges:**
- `operators.count` - Total operators
- `clusters.count` - Total clusters
- `deployments.active` - Active deployments
- `operators.healthy` - Healthy operators count
- `operators.unhealthy` - Unhealthy operators count

### Logging

**Requirements:**
- Structured logging (JSON format)
- Trace ID correlation
- Log levels: Debug, Info, Warning, Error
- Contextual enrichment (operator name, cluster, version)

**Example Log:**
```json
{
  "timestamp": "2025-11-23T12:00:00Z",
  "level": "INFO",
  "message": "Operator deployed successfully",
  "traceId": "abc-123",
  "operatorName": "cert-manager",
  "version": "v1.14.0",
  "cluster": "production-us-east",
  "deploymentId": "deploy-456",
  "userId": "admin@example.com"
}
```

### Health Monitoring

**Requirements:**
- Operator health checks every 30 seconds
- Cluster health checks every 60 seconds
- Controller pod monitoring
- Webhook endpoint monitoring
- CRD reconciliation monitoring

**Health Check Endpoint:**
```
GET /api/v1/operators/{name}/health

Response:
{
  "operatorName": "cert-manager",
  "overallHealth": "Healthy",
  "clusters": [
    {
      "clusterName": "production-us-east",
      "health": "Healthy",
      "controllerPods": 3,
      "webhookStatus": "Available",
      "crdReconciliation": "Active",
      "lastChecked": "2025-11-23T12:00:00Z"
    }
  ]
}
```

---

## Scalability Requirements

### Horizontal Scaling

**Requirements:**
- API supports multiple replicas (stateless design)
- Distributed locking for concurrent deployments
- Cluster client connection pooling
- Database connection pooling

**Scaling Targets:**
```
1 API Instance  → 10 concurrent deployments
3 API Instances → 30 concurrent deployments
10 API Instances → 100 concurrent deployments
```

### Multi-Cluster Management

**Requirements:**
- Support for 100+ Kubernetes clusters
- Parallel health checks across clusters
- Cluster connection caching
- Cluster affinity for operator deployments

### Resource Limits

**Per API Instance:**
- CPU: < 80% sustained
- Memory: < 75% of allocated (4 GB)
- Kubernetes client connections: < 100 concurrent
- Database connections: < 50 concurrent

**Auto-Scaling Triggers:**
- CPU > 70% for 5 minutes → Scale up
- Active deployments > 15 → Scale up
- CPU < 30% for 15 minutes → Scale down

---

## Non-Functional Requirements

### Reliability

- Deployment success rate: 99.9%
- Rollback success rate: 99.9%
- Zero custom resource data loss during upgrades
- Automatic recovery from transient failures

### Maintainability

- Clean code (SOLID principles)
- 85%+ test coverage
- Comprehensive documentation
- Runbooks for common operations

### Testability

- Unit tests for all components
- Integration tests for deployment strategies
- End-to-end tests for multi-cluster scenarios
- Chaos testing for failure scenarios

### Compliance

- Audit logging for all deployments
- Approval workflow for production (SOC 2, ISO 27001)
- Data retention policies
- GDPR compliance (cluster deletion)

---

## Dependencies

### Required Infrastructure

1. **Kubernetes Clusters** - 1+ clusters for operator deployment
2. **PostgreSQL 15+** - Operator metadata, CRD schemas, deployment history
3. **.NET 8.0 Runtime** - Application runtime
4. **Helm 3+** - Operator chart deployment
5. **kubectl** - Kubernetes CLI for cluster operations
6. **Jaeger** - Distributed tracing (optional)
7. **Prometheus** - Metrics collection (optional)

### External Services

1. **Kubernetes API** - Cluster management and operations
2. **Helm Chart Repositories** - Operator chart sources
3. **HashiCorp Vault (self-hosted)** / **Kubernetes Secrets** - Kubeconfig encryption
4. **SMTP Server** - Approval workflow notifications

---

## Acceptance Criteria

**System is production-ready when:**

1. ✅ All functional requirements implemented
2. ✅ 85%+ test coverage achieved
3. ✅ Performance targets met (< 3 min single cluster deploy)
4. ✅ Security requirements satisfied (JWT, HTTPS, RBAC, encryption)
5. ✅ Observability complete (tracing, metrics, logging)
6. ✅ Documentation complete (API docs, deployment guide, runbooks)
7. ✅ Zero-downtime operator upgrade verified
8. ✅ Automated rollback tested and validated
9. ✅ Multi-cluster deployment tested (10+ clusters)
10. ✅ Production deployment successful

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
**Next Review:** After Epic 1 Implementation
**Approval Status:** Pending Architecture Review
