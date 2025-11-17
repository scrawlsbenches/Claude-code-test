# Integration Test Implementation Plan

**Task:** Task #4 from TASK_LIST.md - Integration Test Suite
**Estimated Effort:** 3-4 days
**Status:** In Progress
**Started:** 2025-11-17

---

## Overview

This document outlines the complete implementation plan for adding comprehensive integration tests to the Distributed Kernel Orchestration System using Testcontainers and WebApplicationFactory.

## Goals

1. ✅ Test complete API workflows end-to-end
2. ✅ Test all 4 deployment strategies with real dependencies
3. ✅ Test approval workflow integration
4. ✅ Test audit log persistence to PostgreSQL
5. ✅ Test distributed locking with Redis
6. ✅ Achieve >80% integration path coverage
7. ✅ Integrate into CI/CD pipeline

## Architecture

```
Integration Test Project
├── Fixtures/
│   ├── PostgreSqlContainerFixture.cs      # PostgreSQL Testcontainer lifecycle
│   ├── RedisContainerFixture.cs           # Redis Testcontainer lifecycle
│   └── IntegrationTestFactory.cs          # Custom WebApplicationFactory
├── Helpers/
│   ├── AuthHelper.cs                      # JWT token generation for tests
│   ├── ApiClientHelper.cs                 # HTTP client wrapper
│   └── TestDataBuilder.cs                 # Test data creation
├── Tests/
│   ├── BasicIntegrationTests.cs           # Health, auth, clusters
│   ├── DirectDeploymentIntegrationTests.cs
│   ├── RollingDeploymentIntegrationTests.cs
│   ├── BlueGreenDeploymentIntegrationTests.cs
│   ├── CanaryDeploymentIntegrationTests.cs
│   ├── ApprovalWorkflowIntegrationTests.cs
│   ├── RollbackIntegrationTests.cs
│   ├── ConcurrentDeploymentTests.cs
│   └── FailureRecoveryTests.cs
└── HotSwap.Distributed.IntegrationTests.csproj
```

## Detailed Task Breakdown

### Phase 1: Project Setup (2 hours)

**Tasks:**
1. Create new xUnit test project: `tests/HotSwap.Distributed.IntegrationTests/`
2. Add NuGet packages:
   - `Testcontainers` (core) - v3.10.0+
   - `Testcontainers.PostgreSql` - v3.10.0+
   - `Testcontainers.Redis` - v3.10.0+
   - `Microsoft.AspNetCore.Mvc.Testing` - v8.0.0
   - `xunit` - v2.6.2
   - `xunit.runner.visualstudio` - v2.5.4
   - `Moq` - v4.20.70
   - `FluentAssertions` - v6.12.0
   - `Microsoft.NET.Test.Sdk` - v17.8.0
3. Add project references:
   - HotSwap.Distributed.Api
   - HotSwap.Distributed.Domain
   - HotSwap.Distributed.Infrastructure
   - HotSwap.Distributed.Orchestrator
4. Add to solution file: `dotnet sln add tests/HotSwap.Distributed.IntegrationTests/`
5. Verify build: `dotnet build tests/HotSwap.Distributed.IntegrationTests/`

**Acceptance Criteria:**
- ✅ Project builds successfully
- ✅ All dependencies restored
- ✅ Zero compilation errors

---

### Phase 2: Test Infrastructure (4 hours)

#### 2.1 PostgreSQL Container Fixture (1 hour)

**File:** `Fixtures/PostgreSqlContainerFixture.cs`

**Implementation:**
```csharp
public class PostgreSqlContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; private set; }

    public async Task InitializeAsync()
    {
        Container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("distributed_kernel_test")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }

    public string GetConnectionString() => Container.GetConnectionString();
}
```

#### 2.2 Redis Container Fixture (1 hour)

**File:** `Fixtures/RedisContainerFixture.cs`

**Implementation:**
```csharp
public class RedisContainerFixture : IAsyncLifetime
{
    public RedisContainer Container { get; private set; }

    public async Task InitializeAsync()
    {
        Container = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }

    public string GetConnectionString() => Container.GetConnectionString();
}
```

#### 2.3 Custom WebApplicationFactory (1.5 hours)

**File:** `Fixtures/IntegrationTestFactory.cs`

**Purpose:** Configure API with test containers

**Key Features:**
- Override configuration to use test PostgreSQL
- Override configuration to use test Redis
- Configure in-memory services where needed
- Disable external dependencies (Jaeger, etc.)
- Enable test logging

#### 2.4 Helper Classes (0.5 hour)

**AuthHelper.cs:**
- `GetAdminTokenAsync()` - Get JWT for admin user
- `GetDeployerTokenAsync()` - Get JWT for deployer user
- `GetViewerTokenAsync()` - Get JWT for viewer user

**ApiClientHelper.cs:**
- `CreateDeploymentAsync()` - Helper to create deployment
- `GetDeploymentStatusAsync()` - Poll for deployment status
- `WaitForDeploymentCompletionAsync()` - Wait until deployment completes

**TestDataBuilder.cs:**
- `CreateDeploymentRequest()` - Build test deployment requests
- `CreateTestModule()` - Create test module descriptors

---

### Phase 3: Basic Integration Tests (3 hours)

**File:** `Tests/BasicIntegrationTests.cs`

**Tests:**
1. `HealthCheck_ReturnsHealthy()` - Test /health endpoint
2. `Login_WithValidCredentials_ReturnsToken()` - Test authentication
3. `AuthenticatedRequest_WithValidToken_Succeeds()` - Test auth middleware
4. `ListClusters_ReturnsAllEnvironments()` - Test cluster endpoint
5. `GetClusterInfo_ForDevelopment_ReturnsDetails()` - Test cluster details
6. `CreateDeployment_WithoutAuth_Returns401()` - Test auth requirement
7. `CreateDeployment_WithViewerRole_Returns403()` - Test authorization

**Acceptance Criteria:**
- ✅ All basic API flows work end-to-end
- ✅ Authentication and authorization work
- ✅ Testcontainers start and stop properly
- ✅ Tests run in <30 seconds

---

### Phase 4: Deployment Strategy Tests (8 hours)

#### 4.1 Direct Deployment Test (1.5 hours)

**File:** `Tests/DirectDeploymentIntegrationTests.cs`

**Tests:**
1. `DirectDeployment_ToDevelopment_Succeeds()`
   - Create deployment request (Development environment)
   - Submit deployment
   - Wait for completion
   - Verify success status
   - Verify all nodes deployed
   - Verify audit logs created

#### 4.2 Rolling Deployment Test (2 hours)

**File:** `Tests/RollingDeploymentIntegrationTests.cs`

**Tests:**
1. `RollingDeployment_ToQA_DeploysBatches()`
   - Create deployment request (QA environment)
   - Submit deployment
   - Verify batch-by-batch execution
   - Verify health checks between batches
   - Wait for completion
   - Verify all nodes deployed sequentially

#### 4.3 Blue-Green Deployment Test (2 hours)

**File:** `Tests/BlueGreenDeploymentIntegrationTests.cs`

**Tests:**
1. `BlueGreenDeployment_ToStaging_RequiresApproval()`
   - Create deployment request (Staging environment)
   - Submit deployment
   - Verify deployment paused for approval
   - Approve deployment (as admin)
   - Verify deployment to green environment
   - Verify smoke tests run
   - Verify traffic switch
   - Verify old blue environment exists

#### 4.4 Canary Deployment Test (2.5 hours)

**File:** `Tests/CanaryDeploymentIntegrationTests.cs`

**Tests:**
1. `CanaryDeployment_ToProduction_ProgressiveRollout()`
   - Create deployment request (Production environment)
   - Submit deployment
   - Verify deployment paused for approval
   - Approve deployment (as admin)
   - Verify 10% canary deployment
   - Verify metrics collection
   - Verify progressive rollout (10% → 30% → 50% → 100%)
   - Wait for full completion

---

### Phase 5: Advanced Scenario Tests (6 hours)

#### 5.1 Approval Workflow Test (1.5 hours)

**File:** `Tests/ApprovalWorkflowIntegrationTests.cs`

**Tests:**
1. `ApprovalWorkflow_StagingDeployment_RequiresApproval()`
2. `ApprovalWorkflow_Rejection_CancelsDeployment()`
3. `ApprovalWorkflow_Timeout_AutoRejects()`
4. `ApprovalWorkflow_AdminOnly_CanApprove()`
5. `ApprovalWorkflow_AuditLogged()`

#### 5.2 Rollback Test (1.5 hours)

**File:** `Tests/RollbackIntegrationTests.cs`

**Tests:**
1. `Rollback_AfterFailedDeployment_RestoresPreviousVersion()`
2. `Rollback_FromCanaryDeployment_RevertsToStable()`
3. `Rollback_CreatesAuditLog()`

#### 5.3 Concurrent Deployment Test (1.5 hours)

**File:** `Tests/ConcurrentDeploymentTests.cs`

**Tests:**
1. `ConcurrentDeployments_ToDifferentEnvironments_BothSucceed()`
2. `ConcurrentDeployments_ToSameEnvironment_SecondRejects()`
   - Verify distributed locking with Redis works

#### 5.4 Failure Recovery Test (1.5 hours)

**File:** `Tests/FailureRecoveryTests.cs`

**Tests:**
1. `Deployment_WithNodeFailure_RollsBack()`
2. `Deployment_WithHealthCheckFailure_RollsBack()`
3. `Deployment_WithSignatureValidationFailure_Rejects()`

---

### Phase 6: CI/CD Integration (2 hours)

#### 6.1 Update GitHub Actions Workflow (1 hour)

**File:** `.github/workflows/build-and-test.yml`

**Changes:**
1. Add new job: `integration-tests`
2. Configure Docker-in-Docker for Testcontainers
3. Run integration tests after unit tests
4. Upload integration test results
5. Continue on integration test failure (initially)

**Example:**
```yaml
integration-tests:
  runs-on: ubuntu-latest
  needs: build-and-test

  steps:
  - uses: actions/checkout@v4

  - name: Setup .NET
    uses: actions/setup-dotnet@v4
    with:
      dotnet-version: '8.0.x'

  - name: Restore dependencies
    run: dotnet restore DistributedKernel.sln

  - name: Build
    run: dotnet build DistributedKernel.sln --configuration Release --no-restore

  - name: Run integration tests
    run: dotnet test tests/HotSwap.Distributed.IntegrationTests/ --configuration Release --no-build --verbosity normal
    env:
      TESTCONTAINERS_RYUK_DISABLED: false
```

#### 6.2 Test CI/CD Pipeline (1 hour)

1. Push to branch
2. Verify integration tests run in GitHub Actions
3. Verify containers start and stop properly
4. Verify test results uploaded
5. Fix any CI-specific issues

---

### Phase 7: Documentation (2 hours)

#### 7.1 Update TESTING.md (0.5 hour)

**Additions:**
- Integration test section
- How to run integration tests locally
- How to run specific integration test categories
- Requirements (Docker must be running)
- Troubleshooting Testcontainers

#### 7.2 Create Integration Test README (0.5 hour)

**File:** `tests/HotSwap.Distributed.IntegrationTests/README.md`

**Contents:**
- Purpose and scope
- How to run tests
- Test architecture overview
- Container management
- Debugging integration tests
- Contributing guidelines

#### 7.3 Update Task List (0.5 hour)

**File:** `TASK_LIST.md`

**Changes:**
- Mark Task #4 as ✅ Complete
- Update implementation summary
- Add files created
- Update test count metrics

#### 7.4 Update Documentation Metrics (0.5 hour)

**Files to Update:**
- CLAUDE.md - Update test count
- README.md - Update test badge
- PROJECT_STATUS_REPORT.md - Add integration test stats

---

## Test Execution Strategy

### Local Development

```bash
# Prerequisite: Docker must be running
docker ps

# Run all integration tests
dotnet test tests/HotSwap.Distributed.IntegrationTests/

# Run specific test class
dotnet test --filter "FullyQualifiedName~DirectDeploymentIntegrationTests"

# Run with detailed output
dotnet test tests/HotSwap.Distributed.IntegrationTests/ --verbosity normal

# Run with code coverage
dotnet test tests/HotSwap.Distributed.IntegrationTests/ --collect:"XPlat Code Coverage"
```

### CI/CD Execution

- Integration tests run after unit tests pass
- Testcontainers automatically start PostgreSQL and Redis
- Tests run in parallel where possible
- Containers automatically cleaned up after tests

---

## Success Criteria

- ✅ All 4 deployment strategies tested end-to-end
- ✅ PostgreSQL integration tested (audit logs)
- ✅ Redis integration tested (distributed locks)
- ✅ Approval workflow tested
- ✅ Rollback scenarios tested
- ✅ >80% integration path coverage
- ✅ Tests run in CI/CD pipeline
- ✅ Tests complete in <5 minutes locally
- ✅ Tests complete in <10 minutes in CI/CD
- ✅ Zero flaky tests
- ✅ Documentation complete

---

## Timeline

| Phase | Duration | Deliverable |
|-------|----------|-------------|
| Phase 1: Project Setup | 2 hours | Integration test project created |
| Phase 2: Infrastructure | 4 hours | Fixtures and helpers implemented |
| Phase 3: Basic Tests | 3 hours | 7 basic integration tests passing |
| Phase 4: Strategy Tests | 8 hours | 4 deployment strategy tests passing |
| Phase 5: Advanced Tests | 6 hours | 12 advanced scenario tests passing |
| Phase 6: CI/CD | 2 hours | Integration tests in pipeline |
| Phase 7: Documentation | 2 hours | Complete documentation |
| **Total** | **27 hours** | **~23+ integration tests** |

**Estimated:** 3-4 days (allowing for debugging and refinement)

---

## Risk Mitigation

**Risk 1: Testcontainers slow on CI/CD**
- Mitigation: Use container image caching
- Mitigation: Run integration tests in parallel

**Risk 2: Flaky tests due to timing**
- Mitigation: Use proper async/await patterns
- Mitigation: Add retry logic for timing-sensitive operations
- Mitigation: Increase timeouts for CI/CD vs local

**Risk 3: Docker not available in environment**
- Mitigation: Document Docker requirement clearly
- Mitigation: Provide fallback to in-memory implementations for quick feedback
- Mitigation: Skip integration tests if Docker not available (with warning)

**Risk 4: Tests take too long**
- Mitigation: Optimize container startup (reuse where possible)
- Mitigation: Run tests in parallel
- Mitigation: Use smaller container images (alpine)

---

## Next Steps

1. Begin Phase 1: Create integration test project
2. Add required NuGet packages
3. Create basic test infrastructure
4. Write first integration test (health check)
5. Iterate through phases systematically

---

**Last Updated:** 2025-11-17
**Author:** Claude Code Assistant
**Status:** Planning Complete → Ready for Implementation
