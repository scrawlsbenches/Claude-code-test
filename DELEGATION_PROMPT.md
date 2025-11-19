# Delegation Prompt: Integration Test Recovery

**Created**: 2025-11-19
**Priority**: High
**Estimated Effort**: 6.5-10.5 days
**Status**: Ready for assignment

---

## Context

The integration test suite was recently stabilized after build server crashes. The troubleshooting process achieved a green build (24/69 tests passing, 0 failures) by:
- Replacing Docker/Testcontainers with in-memory alternatives (SQLite, MemoryCache)
- Configuring fast test timeouts (5s canary vs 15min production)
- **Temporarily skipping 45 tests** to unblock CI/CD

**All source code is intact** - no features were lost. The skipped tests need fixes to be re-enabled.

**Reference Documentation:**
- `INTEGRATION_TEST_TROUBLESHOOTING_GUIDE.md` - Complete troubleshooting history
- `TASK_LIST.md` - Task #21-24 details
- `CLAUDE.md` - Development guidelines and pre-commit checklist

---

## Your Mission

Re-enable the **45 skipped integration tests** by fixing the underlying issues. Work through tasks in priority order to maximize test coverage quickly.

---

## Task #1: Fix Rollback Test Assertions (Priority: 🔴 Critical, Effort: 0.5 days)

### Problem
`RollbackScenarioIntegrationTests.cs` - All 8 tests skipped due to HTTP status code mismatch.

**Current Skip Reason:**
```csharp
[Fact(Skip = "API returns 202 Accepted, tests expect 200 OK")]
```

### Root Cause
Tests expect synchronous response (200 OK), but rollback is an async operation that returns 202 Accepted.

### Files to Modify
- `tests/HotSwap.Distributed.IntegrationTests/Tests/RollbackScenarioIntegrationTests.cs`

### What to Do

1. **Update all test assertions** from:
   ```csharp
   response.StatusCode.Should().Be(HttpStatusCode.OK);
   ```
   To:
   ```csharp
   response.StatusCode.Should().Be(HttpStatusCode.Accepted,
       "Rollback is async and returns 202 Accepted");
   ```

2. **Add deployment completion polling** after rollback:
   ```csharp
   // After rollback request
   var response = await _client.PostAsync($"/api/deployments/{deploymentId}/rollback", null);
   response.StatusCode.Should().Be(HttpStatusCode.Accepted);

   // Poll for completion
   var rollbackResult = await response.Content.ReadFromJsonAsync<DeploymentResponse>();
   var completedDeployment = await WaitForDeploymentCompletionAsync(rollbackResult.DeploymentId);

   completedDeployment.Status.Should().Be(DeploymentStatus.Completed.ToString());
   ```

3. **Remove Skip attributes** from all 8 tests:
   - `RollbackSuccessfulDeployment_RestoresPreviousVersion()`
   - `RollbackDeployment_ToMultipleEnvironments_Succeeds()`
   - `RollbackBlueGreenDeployment_SwitchesBackToBlueEnvironment()`
   - `RollbackNonExistentDeployment_Returns404NotFound()`
   - `RollbackInProgressDeployment_ReturnsBadRequestOrConflict()`
   - `Rollback_WithoutAuthentication_Returns401Unauthorized()`
   - `Rollback_WithViewerRole_Returns403Forbidden()`
   - `MultipleSequentialRollbacks_AllSucceed()`

4. **Use existing helper** - `ApiClientHelper.WaitForDeploymentCompletionAsync()` is already implemented

### Acceptance Criteria
- ✅ All 8 rollback tests pass without Skip attribute
- ✅ Tests correctly handle async rollback (202 response)
- ✅ Tests verify final deployment status after completion
- ✅ `dotnet test tests/HotSwap.Distributed.IntegrationTests/` shows 32/69 passing (up from 24)

### Verification
```bash
# Run only rollback tests
dotnet test --filter "FullyQualifiedName~RollbackScenarioIntegrationTests"

# Expected: 8 passed, 0 failed, 0 skipped
```

---

## Task #2: Fix Approval Workflow Test Hang (Priority: 🔴 Critical, Effort: 1-2 days)

### Problem
`ApprovalWorkflowIntegrationTests.cs` - All 7 tests skipped because they hang indefinitely.

**Current Skip Reason:**
```csharp
[Fact(Skip = "Tests hang indefinitely - need investigation")]
```

### Root Cause
**Unknown** - Requires investigation. Possible causes:
1. Approval workflow creates deadlock or infinite wait
2. Event/notification not firing to resume deployment
3. Timeout configuration issue
4. Database/cache state issue

### Files to Investigate
- `tests/HotSwap.Distributed.IntegrationTests/Tests/ApprovalWorkflowIntegrationTests.cs`
- `src/HotSwap.Distributed.Infrastructure/Services/ApprovalService.cs`
- `src/HotSwap.Distributed.Orchestrator/DeploymentPipeline.cs` (approval pause logic)
- `src/HotSwap.Distributed.Api/Controllers/DeploymentsController.cs` (approval endpoints)

### Investigation Steps

1. **Run single test with debugging**:
   ```bash
   dotnet test --filter "Deployment_RequiringApproval_CreatesPendingApprovalRequest" -v detailed
   ```
   - Note where test hangs (check last log message)
   - Use debugger breakpoints in ApprovalService

2. **Check approval workflow logic**:
   - Does `DeploymentPipeline` pause at correct stage?
   - Does approval request get created in database/cache?
   - Does approval/rejection trigger pipeline resume?

3. **Review timeout configuration**:
   ```csharp
   // In IntegrationTestFactory.cs
   ["Pipeline:ApprovalTimeoutHours"] = "1", // Should be sufficient
   ```

4. **Add test timeout attribute**:
   ```csharp
   [Fact(Timeout = 30000)] // 30 second timeout
   public async Task Deployment_RequiringApproval_CreatesPendingApprovalRequest()
   ```

### What to Do

**Phase 1: Identify hang location** (Day 1)
- Add extensive logging to approval workflow
- Run test with timeout to force failure
- Analyze logs to find exact hang point

**Phase 2: Fix identified issue** (Day 1-2)
- Fix deadlock/infinite wait
- Ensure approval request creation completes
- Ensure approval/rejection resumes pipeline
- Add safeguards (timeouts, circuit breakers)

**Phase 3: Re-enable tests** (Day 2)
- Remove Skip attributes from all 7 tests
- Verify each test individually
- Verify full suite together

### Acceptance Criteria
- ✅ Identified root cause of hang (documented in commit message)
- ✅ All 7 approval tests pass without Skip attribute
- ✅ Tests complete in <10 seconds each
- ✅ No deadlocks or infinite waits
- ✅ `dotnet test tests/HotSwap.Distributed.IntegrationTests/` shows 39/69 passing

### Verification
```bash
# Run only approval tests with timeout
dotnet test --filter "FullyQualifiedName~ApprovalWorkflowIntegrationTests"

# Expected: 7 passed, 0 failed, 0 skipped, all completing in <10s
```

---

## Task #3: Optimize Slow Deployment Tests (Priority: 🟡 High, Effort: 2-3 days)

### Problem
Two test files skipped due to slow execution (>30 seconds per test):
- `ConcurrentDeploymentIntegrationTests.cs` - 7 tests
- `DeploymentStrategyIntegrationTests.cs` - 9 tests

**Current Skip Reason:**
```csharp
[Fact(Skip = "Tests too slow (>30s) - need optimization")]
```

### Root Cause
Complex deployment scenarios take too long even with fast configuration:
- Multiple concurrent deployments
- Multi-stage pipelines (Build → QA → Staging → Production)
- Canary rollouts with multiple increments
- Blue-Green environment switches

### Files to Optimize
- `tests/HotSwap.Distributed.IntegrationTests/Tests/ConcurrentDeploymentIntegrationTests.cs`
- `tests/HotSwap.Distributed.IntegrationTests/Tests/DeploymentStrategyIntegrationTests.cs`
- `tests/HotSwap.Distributed.IntegrationTests/Fixtures/IntegrationTestFactory.cs`

### Current Fast Configuration
```csharp
// IntegrationTestFactory.cs - Already configured
["Pipeline:CanaryWaitDuration"] = "00:00:05", // 5 seconds
["Pipeline:StagingSmokeTestTimeout"] = "00:00:10", // 10 seconds
["Pipeline:CanaryIncrementPercentage"] = "50", // 50% increments
```

### Investigation Steps

1. **Profile slow tests**:
   ```bash
   # Run with timing
   dotnet test --filter "ConcurrentDeployments_ToDifferentEnvironments_AllSucceed" -v detailed

   # Identify slowest operations
   ```

2. **Check what's causing delays**:
   - Stage transitions (Build → QA → Staging → Production)
   - Canary wait durations (even 5s × multiple increments = slow)
   - Polling intervals for deployment completion
   - Database/cache operations

3. **Review test complexity**:
   - Can tests be simplified?
   - Can stages be reduced for testing?
   - Can timeouts be further reduced without breaking tests?

### Optimization Strategies

**Option 1: Ultra-fast test configuration** (Try first)
```csharp
// Create test-specific ultra-fast config
["Pipeline:CanaryWaitDuration"] = "00:00:01", // 1 second (vs 5s)
["Pipeline:StagingSmokeTestTimeout"] = "00:00:02", // 2 seconds (vs 10s)
["Pipeline:StageTransitionDelay"] = "00:00:00", // No delay
```

**Option 2: Simplify test scenarios**
- Reduce number of canary increments (2 increments vs 10)
- Test with fewer stages (Build → Production only)
- Use minimal deployment payloads

**Option 3: Mock slow operations**
- Mock stage execution (validation, smoke tests)
- Mock health checks
- Mock deployment status updates

**Option 4: Parallel test execution**
- Use xUnit parallel execution for ConcurrentDeployment tests
- Ensure tests are properly isolated

### What to Do

**Phase 1: Profile and identify bottlenecks** (Day 1)
- Run slow tests with detailed logging
- Measure time spent in each operation
- Identify top 3 slowest operations

**Phase 2: Apply optimizations** (Day 2)
- Implement fastest viable configuration
- Simplify test scenarios if needed
- Mock slow operations where appropriate

**Phase 3: Verify and re-enable** (Day 2-3)
- Remove Skip attributes from all 16 tests
- Verify tests complete in <10 seconds each
- Ensure tests still validate correctness

### Acceptance Criteria
- ✅ All 16 tests pass without Skip attribute
- ✅ Each test completes in <10 seconds (target: <5 seconds)
- ✅ Tests still validate deployment correctness
- ✅ No flaky failures due to aggressive timeouts
- ✅ `dotnet test tests/HotSwap.Distributed.IntegrationTests/` shows 55/69 passing

### Verification
```bash
# Run deployment strategy tests
dotnet test --filter "FullyQualifiedName~DeploymentStrategyIntegrationTests"
# Expected: 9 passed, 0 failed, 0 skipped, all <10s

# Run concurrent deployment tests
dotnet test --filter "FullyQualifiedName~ConcurrentDeploymentIntegrationTests"
# Expected: 7 passed, 0 failed, 0 skipped, all <10s
```

---

## Task #4: Wire Up Multi-Tenant API Endpoints (Priority: 🟢 Medium, Effort: 3-4 days)

### Problem
`MultiTenantIntegrationTests.cs` - All 14 tests skipped because API endpoints return 404.

**Current Skip Reason:**
```csharp
[Fact(Skip = "Multi-tenant API endpoints not yet implemented - return 404")]
```

### Root Cause
**Source code exists but endpoints not wired up**. Investigation found:
- ✅ `TenantsController.cs` exists
- ✅ `TenantDeploymentsController.cs` exists
- ✅ Domain models exist (`Tenant.cs`, `TenantSubscription.cs`)
- ✅ Services exist (`TenantProvisioningService.cs`, etc.)
- ❓ Controllers not registered or routing misconfigured

### Files to Investigate
- `src/HotSwap.Distributed.Api/Program.cs` - Check controller registration
- `src/HotSwap.Distributed.Api/Controllers/TenantsController.cs` - Verify routing
- `src/HotSwap.Distributed.Api/Controllers/TenantDeploymentsController.cs`
- `src/HotSwap.Distributed.Infrastructure/Tenants/InMemoryTenantRepository.cs`

### Investigation Steps

1. **Check if controllers are registered**:
   ```bash
   grep -r "TenantsController\|TenantDeploymentsController" src/HotSwap.Distributed.Api/Program.cs
   ```

2. **Verify routing configuration**:
   ```csharp
   // In TenantsController.cs - check route attributes
   [ApiController]
   [Route("api/[controller]")]
   public class TenantsController : ControllerBase
   ```

3. **Test endpoints manually**:
   ```bash
   # Start API
   dotnet run --project src/HotSwap.Distributed.Api/

   # Test tenant creation
   curl -X POST http://localhost:5000/api/tenants \
     -H "Content-Type: application/json" \
     -d '{"name":"Test","subdomain":"test"}'
   ```

4. **Check dependency injection**:
   ```bash
   # Verify services are registered in Program.cs
   grep -r "ITenantRepository\|ITenantProvisioningService" src/HotSwap.Distributed.Api/Program.cs
   ```

### What to Do

**Phase 1: Identify why endpoints return 404** (Day 1)
- Check controller registration in Program.cs
- Check routing attributes on controllers
- Check service dependency injection
- Test endpoints with curl/Postman

**Phase 2: Wire up missing pieces** (Day 2-3)
- Register controllers if missing
- Fix routing configuration if incorrect
- Register required services in DI container
- Verify authentication/authorization requirements

**Phase 3: Implement missing features** (Day 3-4)
- If controllers are stubs, implement CRUD operations
- Connect controllers to services
- Add validation and error handling
- Ensure tenant context middleware is enabled

**Phase 4: Re-enable tests** (Day 4)
- Remove Skip attributes from all 14 tests
- Run tests individually to verify
- Fix any remaining issues

### Acceptance Criteria
- ✅ All tenant API endpoints return valid responses (not 404)
- ✅ All 14 multi-tenant tests pass without Skip attribute
- ✅ Tenant CRUD operations work correctly
- ✅ Tenant isolation enforced (deployments scoped to tenant)
- ✅ `dotnet test tests/HotSwap.Distributed.IntegrationTests/` shows 69/69 passing

### Verification
```bash
# Run only multi-tenant tests
dotnet test --filter "FullyQualifiedName~MultiTenantIntegrationTests"

# Expected: 14 passed, 0 failed, 0 skipped

# Verify full integration test suite
dotnet test tests/HotSwap.Distributed.IntegrationTests/

# Expected: 69/69 passed, 0 failed, 0 skipped
```

---

## Success Criteria (All Tasks Complete)

### Test Coverage Restored
- ✅ **69/69 integration tests passing** (100% coverage)
- ✅ **0 tests skipped** (0% technical debt)
- ✅ **0 tests failing** (green build maintained)

### Test Performance
- ✅ Full integration test suite completes in **<2 minutes**
- ✅ Individual tests complete in **<10 seconds each**
- ✅ No test timeouts or hangs

### Code Quality
- ✅ All test assertions accurate (202 vs 200 fixed)
- ✅ All API endpoints functional (multi-tenant wired up)
- ✅ All workflows non-blocking (approval hang fixed)
- ✅ All tests optimized (deployment scenarios <10s)

### Documentation
- ✅ Update `TASK_LIST.md` - Mark Tasks #21-24 as ✅ Completed
- ✅ Update `INTEGRATION_TEST_TROUBLESHOOTING_GUIDE.md` - Add resolution notes
- ✅ Update `CLAUDE.md` - Update test count if changed
- ✅ Commit messages reference task numbers (e.g., "fix: resolve rollback test assertions (Task #21)")

---

## Pre-Commit Checklist (MANDATORY)

**Before EVERY commit, run:**

```bash
# Step 1: Clean build
dotnet clean

# Step 2: Restore packages
dotnet restore

# Step 3: Build entire solution
dotnet build --no-incremental

# Expected: Build succeeded. 0 Warning(s) 0 Error(s)

# Step 4: Run ALL tests (not just integration tests)
dotnet test

# Expected: 568 unit tests + X integration tests, 0 failures, 0 skipped

# Step 5: Only if ALL steps succeed, commit
git add .
git commit -m "fix: your message here (Task #XX)"

# Step 6: Push to feature branch
git push -u origin claude/your-branch-name
```

**If ANY step fails → DO NOT COMMIT. Fix errors first.**

---

## Technical Notes

### Test Infrastructure
- **Factory**: `IntegrationTestFactory` (WebApplicationFactory with in-memory services)
- **Database**: SQLite in-memory (`:memory:`) - connection must stay open
- **Cache**: `MemoryDistributedCache` (built-in .NET)
- **Locking**: `InMemoryDistributedLock` (custom implementation)
- **Auth**: `AuthHelper.GetAdminTokenAsync()` for JWT tokens

### Key Helpers
- `ApiClientHelper.WaitForDeploymentCompletionAsync()` - Poll for deployment completion
- `AuthHelper.GetAdminTokenAsync()` - Get admin JWT token
- `AuthHelper.GetDeployerTokenAsync()` - Get deployer JWT token
- `AuthHelper.GetViewerTokenAsync()` - Get viewer JWT token
- `TestDataBuilder.CreateDeploymentRequest()` - Build test deployment requests

### Configuration
```csharp
// Fast test timeouts already configured in IntegrationTestFactory.cs
CanaryWaitDuration = 5 seconds (vs 15 minutes production)
StagingSmokeTestTimeout = 10 seconds (vs 5 minutes production)
CanaryIncrementPercentage = 50% (vs 20% production)
ApprovalTimeoutHours = 1 hour (vs 24 hours production)
```

### Common Patterns
```csharp
// 1. Create authenticated client
var client = _fixture.Factory.CreateClient();
var authHelper = new AuthHelper(client);
var token = await authHelper.GetAdminTokenAsync();
authHelper.AddAuthorizationHeader(client, token);

// 2. Create deployment
var request = TestDataBuilder.CreateDeploymentRequest(strategy: DeploymentStrategy.Canary);
var response = await client.PostAsJsonAsync("/api/deployments", request);

// 3. Wait for completion
var deployment = await response.Content.ReadFromJsonAsync<DeploymentResponse>();
var completed = await ApiClientHelper.WaitForDeploymentCompletionAsync(client, deployment.DeploymentId);

// 4. Assert results
completed.Status.Should().Be(DeploymentStatus.Completed.ToString());
```

---

## Questions?

**Reference these documents:**
- `INTEGRATION_TEST_TROUBLESHOOTING_GUIDE.md` - Full troubleshooting history
- `TASK_LIST.md` - Task details and context
- `CLAUDE.md` - Development guidelines, pre-commit checklist
- `README.md` - Project overview
- `TESTING.md` - Testing strategy

**If you encounter issues:**
1. Check the troubleshooting guide for similar issues
2. Review git history: `git log --oneline --since="2 weeks ago"`
3. Check CI/CD logs for patterns
4. Ask for clarification with specific error messages

---

## Priority Order

Execute tasks in this order for maximum impact:

1. **Task #1** (0.5 days) → 8 tests restored → 32/69 passing (46%)
2. **Task #2** (1-2 days) → 7 tests restored → 39/69 passing (57%)
3. **Task #3** (2-3 days) → 16 tests restored → 55/69 passing (80%)
4. **Task #4** (3-4 days) → 14 tests restored → **69/69 passing (100%)**

**Total time**: 6.5-10.5 days to full coverage

---

**Good luck! Remember: Test-driven, build-first, commit when green. 🚀**
