# Integration Test Debugger Skill

**Version:** 1.0.0
**Last Updated:** 2025-11-20
**Skill Type:** Quality & Testing
**Estimated Time:** 30-90 minutes per test suite
**Complexity:** Medium-High

---

## Purpose

This skill guides you through systematic debugging of integration test failures, with specific focus on common issues in .NET integration tests: hanging tests, timeouts, assertion failures, and dependency issues.

**Use this skill when:**
- Integration tests are hanging indefinitely
- Tests timeout after 30+ seconds
- Tests fail with assertion errors (HTTP 200 vs 202, 404, etc.)
- Tests pass locally but fail in CI/CD
- Tests have intermittent failures (flaky tests)

**This skill addresses:**
- Task #23: ApprovalWorkflow tests hanging (7 tests)
- Task #24: Slow deployment tests (16 tests timing out)
- Task #22: Multi-tenant tests returning 404 (14 tests)

---

## Prerequisites

**Before using this skill:**
- [ ] Integration test project exists (`tests/HotSwap.Distributed.IntegrationTests/`)
- [ ] You can run individual integration tests (`dotnet test --filter "FullyQualifiedName~TestName"`)
- [ ] You understand the expected behavior of the failing tests

**Required tools:**
- .NET SDK 8.0+
- Test runner (dotnet test or IDE)
- Optional: Profiler (dotnet-trace, Visual Studio Profiler)

---

## Phase 1: Categorize the Failure

### Step 1.1: Identify Failure Pattern

Run the failing test in isolation and observe the behavior:

```bash
# Run single test with verbose output
dotnet test tests/HotSwap.Distributed.IntegrationTests/ \
  --filter "FullyQualifiedName~TestName" \
  --logger "console;verbosity=detailed"
```

**Categorize the failure:**

- [ ] **Hanging (no output, never completes)** í Go to Phase 2
- [ ] **Timeout (fails after 30s-5min)** í Go to Phase 3
- [ ] **Assertion failure (expected vs actual mismatch)** í Go to Phase 4
- [ ] **HTTP error (404, 401, 403, 500)** í Go to Phase 5
- [ ] **Exception (NullReferenceException, etc.)** í Go to Phase 6

### Step 1.2: Check Test Status in TASK_LIST.md

```bash
grep -A 20 "Integration Test Fixes" TASK_LIST.md
```

**Check if the test is documented:**
- [ ] Test is in Task #21 (Rollback - HTTP 202) í Fixed, use as reference
- [ ] Test is in Task #22 (Multi-tenant - 404) í Endpoints not implemented
- [ ] Test is in Task #23 (ApprovalWorkflow - hanging) í Known issue
- [ ] Test is in Task #24 (Deployment/Concurrent - slow) í Optimization needed

---

## Phase 2: Debug Hanging Tests

**Symptoms:** Test runs indefinitely, no output, never completes.

### Step 2.1: Add Timeout to Test

```csharp
[Fact(Timeout = 30000)] // 30 seconds
public async Task TestName()
{
    // Test implementation
}
```

Run the test again. If it now times out instead of hanging, you've confirmed it's a blocking operation.

### Step 2.2: Check for Async/Await Issues

**Common causes of hanging tests:**

**L Deadlock: Missing await**
```csharp
// WRONG - Blocks indefinitely
var result = SomeAsyncMethod().Result; // Deadlock!

// CORRECT - Use await
var result = await SomeAsyncMethod();
```

**L Deadlock: ConfigureAwait(false) missing in library code**
```csharp
// Library code should use:
await SomeAsyncMethod().ConfigureAwait(false);
```

**L Infinite loop or wait**
```csharp
// WRONG - Waits forever if condition never true
while (deployment.Status == DeploymentStatus.Pending)
{
    await Task.Delay(TimeSpan.FromSeconds(30)); // No timeout!
    deployment = await GetDeploymentAsync(id);
}

// CORRECT - Add timeout
var timeout = DateTime.UtcNow.AddMinutes(5);
while (deployment.Status == DeploymentStatus.Pending)
{
    if (DateTime.UtcNow > timeout)
        throw new TimeoutException("Deployment did not complete in 5 minutes");

    await Task.Delay(TimeSpan.FromSeconds(1));
    deployment = await GetDeploymentAsync(id);
}
```

### Step 2.3: Profile the Hanging Test

Use dotnet-trace to see where the test is stuck:

```bash
# Start profiling
dotnet-trace collect --process-id $(pgrep -f dotnet) --providers Microsoft-Diagnostics-DiagnosticSource

# In another terminal, run the test
dotnet test --filter "FullyQualifiedName~HangingTest"

# Wait 30 seconds, then stop profiling (Ctrl+C)
# Open trace file in Visual Studio or PerfView
```

**Look for:**
- Thread waiting on Monitor.Enter (deadlock)
- Thread blocked on Task.Result or Task.Wait
- Thread waiting on ManualResetEvent or Semaphore

### Step 2.4: Fix ApprovalWorkflow Hanging (Task #23 Specific)

**Known issue:** ApprovalWorkflow tests wait indefinitely for approval.

**Root cause:** Test waits for approval but approval service is not mocked or configured.

**Fix pattern:**
```csharp
// BEFORE (hangs)
var deployment = await CreateDeploymentAsync(
    "test-module", "1.0.0", "Production" // Requires approval
);
// Test hangs here waiting for approval that never comes

// AFTER (works)
// Option 1: Mock the approval service
var mockApprovalService = new Mock<IApprovalService>();
mockApprovalService
    .Setup(x => x.CreateApprovalRequestAsync(It.IsAny<string>(), It.IsAny<Environment>(),
        It.IsAny<ModuleDescriptor>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(new ApprovalRequest
    {
        Status = ApprovalStatus.Approved // Auto-approve
    });

// Option 2: Use Development environment (no approval required)
var deployment = await CreateDeploymentAsync(
    "test-module", "1.0.0", "Development" // No approval required
);

// Option 3: Approve in test immediately after creation
var deployment = await CreateDeploymentAsync(
    "test-module", "1.0.0", "Production"
);
await ApproveDeploymentAsync(deployment.ExecutionId); // Approve immediately
await WaitForCompletionAsync(deployment.ExecutionId, timeout: TimeSpan.FromMinutes(2));
```

---

## Phase 3: Debug Timeout Tests

**Symptoms:** Test fails after 30 seconds or more with timeout error.

### Step 3.1: Identify Slow Operations

Add logging to measure time:

```csharp
[Fact]
public async Task SlowTest()
{
    var stopwatch = Stopwatch.StartNew();

    _testOutputHelper.WriteLine($"[{stopwatch.Elapsed}] Starting test");

    var deployment = await CreateDeploymentAsync(...);
    _testOutputHelper.WriteLine($"[{stopwatch.Elapsed}] Deployment created");

    await WaitForCompletion(deployment.ExecutionId);
    _testOutputHelper.WriteLine($"[{stopwatch.Elapsed}] Deployment completed");

    Assert.Equal(DeploymentStatus.Completed, deployment.Status);
    _testOutputHelper.WriteLine($"[{stopwatch.Elapsed}] Test completed");
}
```

**Run the test and analyze the output:**
```
[00:00:00.1234] Starting test
[00:00:02.5678] Deployment created
[00:00:32.9999] Deployment completed ê Took 30 seconds!
[00:00:33.1234] Test completed
```

### Step 3.2: Reduce Deployment Timeouts for Tests

**Problem:** Production deployment takes 15 minutes, test times out.

**Solution:** Override timeouts in test configuration.

**Create test-specific appsettings:**
```json
// tests/HotSwap.Distributed.IntegrationTests/appsettings.IntegrationTests.json
{
  "DeploymentPipeline": {
    "CanaryWaitDuration": "00:00:05",        // 5 seconds (vs 5 minutes prod)
    "StagingSmokeTestTimeout": "00:00:10",   // 10 seconds (vs 5 minutes prod)
    "CanaryIncrementPercentage": 50,         // 50% (vs 20% prod)
    "MaxDeploymentDuration": "00:01:00"      // 1 minute (vs 30 minutes prod)
  },
  "HealthCheck": {
    "HeartbeatInterval": "00:00:01",         // 1 second (vs 30 seconds prod)
    "HeartbeatTimeout": "00:00:05"           // 5 seconds (vs 2 minutes prod)
  }
}
```

**Load test configuration in test fixture:**
```csharp
public class IntegrationTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.IntegrationTests.json");
        });

        // ... other configuration
    }
}
```

### Step 3.3: Mock Time-Based Delays

**Problem:** `Task.Delay(TimeSpan.FromMinutes(5))` takes 5 minutes in tests.

**Solution:** Use ITimeProvider abstraction (or mock time).

**Create time provider interface:**
```csharp
public interface ITimeProvider
{
    DateTime UtcNow { get; }
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}

public class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        => Task.Delay(delay, cancellationToken);
}

public class MockTimeProvider : ITimeProvider
{
    private DateTime _currentTime = DateTime.UtcNow;

    public DateTime UtcNow => _currentTime;

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        _currentTime += delay; // Fast-forward time instead of waiting
        return Task.CompletedTask;
    }

    public void Advance(TimeSpan duration) => _currentTime += duration;
}
```

**Use in production code:**
```csharp
public class CanaryDeploymentStrategy
{
    private readonly ITimeProvider _timeProvider;

    public CanaryDeploymentStrategy(ITimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public async Task DeployAsync(...)
    {
        // Deploy to 10%
        await DeployToNodesAsync(...);

        // Wait 5 minutes (or fast-forward in tests)
        await _timeProvider.DelayAsync(TimeSpan.FromMinutes(5), cancellationToken);

        // Check metrics
        var metrics = await GetMetricsAsync(...);
    }
}
```

**Use in tests:**
```csharp
[Fact]
public async Task CanaryDeployment_FastTest()
{
    // Use mock time provider
    var mockTime = new MockTimeProvider();
    services.AddSingleton<ITimeProvider>(mockTime);

    // Start deployment
    var deployment = await CreateCanaryDeploymentAsync(...);

    // Fast-forward time instead of waiting
    mockTime.Advance(TimeSpan.FromMinutes(5));

    // Verify deployment progressed
    var status = await GetDeploymentStatusAsync(deployment.ExecutionId);
    Assert.Equal(DeploymentStatus.Completed, status.Status);
}
```

### Step 3.4: Reduce Node Count for Tests

**Problem:** Deploying to 20 production nodes takes too long.

**Solution:** Use fewer nodes in test clusters.

```csharp
// Test fixture configuration
public IntegrationTestFactory()
{
    _developmentCluster = new EnvironmentCluster(
        Environment.Development,
        CreateNodes(2) // 2 nodes instead of 3
    );

    _productionCluster = new EnvironmentCluster(
        Environment.Production,
        CreateNodes(4) // 4 nodes instead of 20
    );
}
```

---

## Phase 4: Debug Assertion Failures

**Symptoms:** Test fails with assertion error (expected vs actual mismatch).

### Step 4.1: Common HTTP Status Code Mismatches

**Example:** Task #21 (Rollback tests) - Expected 200 OK, got 202 Accepted.

**Root cause:** Async operations return 202 Accepted, not 200 OK.

**Fix pattern:**
```csharp
// BEFORE (fails)
var response = await _client.PostAsync($"/api/v1/deployments/{id}/rollback", null);
Assert.Equal(HttpStatusCode.OK, response.StatusCode); // Fails: expected 200, got 202

// AFTER (passes)
var response = await _client.PostAsync($"/api/v1/deployments/{id}/rollback", null);
Assert.Equal(HttpStatusCode.Accepted, response.StatusCode); // Passes: 202 is correct
```

**Common patterns:**
- `POST /api/v1/deployments` í Returns **202 Accepted** (async operation)
- `POST /api/v1/deployments/{id}/rollback` í Returns **202 Accepted** (async operation)
- `GET /api/v1/deployments/{id}` í Returns **200 OK** (sync query)
- `POST /api/v1/approvals/{id}/approve` í Returns **200 OK** or **204 No Content**

---

## Success Criteria

You've successfully debugged integration tests when:

- [ ] All tests in the suite pass (0 failures)
- [ ] Tests complete in reasonable time (<5 minutes total)
- [ ] No tests are skipped (unless documented as blocked by missing feature)
- [ ] Tests are stable (pass consistently on multiple runs)
- [ ] Tests pass in both local and CI/CD environments
- [ ] TASK_LIST.md updated with completion status
- [ ] Fix documented in test comments or commit message

---

## Quick Reference: Test Debugging Commands

```bash
# Run single test with detailed output
dotnet test --filter "FullyQualifiedName~TestName" --logger "console;verbosity=detailed"

# Run all tests in a file
dotnet test --filter "FullyQualifiedName~ApprovalWorkflowIntegrationTests"

# Run all integration tests
dotnet test tests/HotSwap.Distributed.IntegrationTests/

# Profile hanging test
dotnet-trace collect --process-id $(pgrep -f dotnet)

# Check API logs
docker-compose logs orchestrator-api | grep ERROR
```

---

**Last Updated:** 2025-11-20
**Version:** 1.0.0
**Unblocks:** Tasks #23, #24 (45+ integration tests)
**Estimated Time Saved:** 2-4 days of trial-and-error debugging
