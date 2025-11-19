# Race Condition Investigation Skill

**Description**: Specialized skill for debugging async/await race conditions and timing issues in .NET applications.

**When to use**:
- Tests pass locally but fail on CI/CD
- Intermittent test failures
- KeyNotFoundException or NullReferenceException in async code
- "Sometimes works, sometimes doesn't" behavior

## Investigation Framework

This skill implements a systematic approach to identifying and fixing race conditions, based on lessons learned from the RollbackBlueGreenDeployment fix.

### Phase 1: Reproduce and Characterize

#### Step 1.1: Environment Comparison

```bash
# Run test multiple times locally
for i in {1..5}; do
    echo "=== Run $i ==="
    dotnet test --filter "FullyQualifiedName~[TestName]" --verbosity normal
done
```

**Document**:
- ✅ Does it pass locally? How consistently?
- ❌ Does it fail on CI/CD? How often?
- ⏱️ How long does it take to execute?
- 🔄 Is failure rate affected by system load?

#### Step 1.2: Timing Analysis

Add diagnostic logging to track timing:
```csharp
_logger.LogInformation("Operation A started at {Time}", DateTime.UtcNow);
await SomeAsyncOperation();
_logger.LogInformation("Operation A completed at {Time}", DateTime.UtcNow);
```

Look for:
- Unexpected ordering of operations
- Long delays between expected sequential operations
- Operations completing "too fast" (skipped/cached)

### Phase 2: Identify Race Condition Patterns

#### Pattern 1: Task.Run() Fire-and-Forget

**Search for**:
```bash
grep -r "_ = Task.Run" src/
grep -r "Task.Run.*async.*=>" src/
```

**Red flags**:
- Background tasks that modify shared state
- No synchronization between foreground and background
- Operations executed out of expected order

**Example from our fix**:
```csharp
// ❌ WRONG: Operations in wrong order
await RemoveInProgressAsync(...);  // Marks done
await StoreResultAsync(...);        // Stores result

// ✅ CORRECT: Store before marking done
await StoreResultAsync(...);        // Store result
await RemoveInProgressAsync(...);   // Then mark done
```

#### Pattern 2: Missing Status Updates

**Search for**:
```bash
grep -r "UpdateStatus\|SetStatus\|Status.*=" src/
```

**Red flags**:
- Status never set to final state (Succeeded/Failed)
- Status checks that don't account for all states
- Polling loops that check incomplete status set

**Example from our fix**:
```csharp
// ❌ MISSING: No final status update
result.EndTime = DateTime.UtcNow;
return result;  // Status still "Running"!

// ✅ ADDED: Final status update
await UpdatePipelineStateAsync(
    request,
    result.Success ? "Succeeded" : "Failed",
    "Completed",
    result.StageResults);
```

#### Pattern 3: Cache/State Lookup Timing

**Check**:
```bash
# Find cache operations
grep -r "GetAsync\|SetAsync\|TryGetValue" src/

# Check DI registrations
grep -r "AddSingleton\|AddScoped\|AddTransient" src/
```

**Red flags**:
- Scoped services in singleton services (wrong lifetime)
- Cache checked before set completes
- No retry logic for transient failures

**Example from our fix**:
```csharp
// ✅ ADDED: Retry logic for timing issues
PipelineExecutionResult? result = null;
for (int attempt = 1; attempt <= 3; attempt++)
{
    result = await _deploymentTracker.GetResultAsync(executionId);
    if (result != null) break;

    if (attempt < 3)
        await Task.Delay(100, cancellationToken);
}
```

### Phase 3: Verify Dependency Injection Lifetimes

```bash
# Check service registrations
grep -rn "AddSingleton\|AddScoped\|AddTransient" src/ | grep -i "tracker\|repository\|service\|manager"
```

**Verify**:
- ✅ Shared state services are Singleton (not Scoped)
- ✅ No Scoped services injected into Singletons
- ✅ Transient services don't hold state

**Common mistake**:
```csharp
// ❌ WRONG: Shared cache as Scoped
services.AddScoped<IDeploymentTracker, InMemoryDeploymentTracker>();

// ✅ CORRECT: Shared cache as Singleton
services.AddSingleton<IDeploymentTracker, InMemoryDeploymentTracker>();
```

### Phase 4: Async/Await Anti-Patterns

Search for common async mistakes:

```bash
# Find potential issues
grep -r "async void" src/                    # Should be async Task
grep -r "\.Result\|\.Wait()" src/            # Deadlock risk
grep -r "Task.Run.*async.*Task" src/        # Double async wrapping
```

**Check for**:
- `async void` methods (should be `async Task`)
- Blocking on async code (`.Result`, `.Wait()`)
- Missing `ConfigureAwait(false)` in library code
- Not awaiting async operations

### Phase 5: Implement Defensive Fixes

Based on findings, apply multiple layers of defense:

#### Layer 1: Fix Ordering
Ensure operations happen in correct sequence:
```csharp
// Store state BEFORE marking complete
await StoreResultAsync();
await MarkCompleteAsync();
```

#### Layer 2: Add Status Updates
Ensure all state transitions are captured:
```csharp
// Update to final status
await UpdateStatusAsync(finalStatus);
```

#### Layer 3: Add Retry Logic
Handle transient timing issues:
```csharp
// Retry with exponential backoff
for (int i = 0; i < 3; i++)
{
    var result = await TryGetAsync();
    if (result != null) return result;
    await Task.Delay(100 * (i + 1));
}
```

#### Layer 4: Add Timeout Protection
Prevent indefinite waits:
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await operationAsync(cts.Token);
```

### Phase 6: Testing Strategy

#### Local Testing
```bash
# Run test repeatedly to check stability
for i in {1..10}; do
    dotnet test --filter "FullyQualifiedName~[TestName]"
    if [ $? -ne 0 ]; then
        echo "Failed on iteration $i"
        break
    fi
done
```

#### CI/CD Verification
- Push to branch and monitor GitHub Actions
- Check for consistent passes across multiple runs
- Verify timing under load

### Phase 7: Documentation

Document the race condition in code:
```csharp
// Store result BEFORE removing in-progress to avoid race condition
// where rollback checks for result before it's stored (Issue #XX)
await _deploymentTracker.StoreResultAsync(id, result);
await _deploymentTracker.RemoveInProgressAsync(id);
```

## Common Race Condition Scenarios

### Scenario 1: Producer-Consumer Mismatch
**Symptom**: Consumer can't find item producer just created
**Fix**: Ensure producer completes before consumer starts

### Scenario 2: Status Polling Miss
**Symptom**: Poller never sees intermediate status
**Fix**: Add final status update or increase polling frequency

### Scenario 3: Cache Timing
**Symptom**: Item not found immediately after being stored
**Fix**: Ensure synchronous completion or add retry logic

### Scenario 4: Background Task Race
**Symptom**: Foreground code expects background task state
**Fix**: Use proper synchronization (await, lock, semaphore)

## Debugging Tools

### Add Timing Logs
```csharp
var sw = Stopwatch.StartNew();
_logger.LogDebug("Starting operation at {Elapsed}ms", sw.ElapsedMilliseconds);
await operation();
_logger.LogDebug("Completed operation at {Elapsed}ms", sw.ElapsedMilliseconds);
```

### Check Thread IDs
```csharp
_logger.LogDebug("Executing on thread {ThreadId}",
    Thread.CurrentThread.ManagedThreadId);
```

### Monitor Task State
```csharp
_logger.LogDebug("Task status: {Status}, IsCompleted: {IsCompleted}",
    task.Status, task.IsCompleted);
```

## Red Flags Checklist

When investigating, look for these warning signs:

- ❌ `Task.Run()` without proper await/synchronization
- ❌ Async operations without status updates
- ❌ Cache lookups without retry logic
- ❌ Background tasks modifying shared state
- ❌ Operations assuming synchronous completion
- ❌ Scoped services in Singleton contexts
- ❌ Tests passing locally but failing remotely
- ❌ Intermittent failures with no clear pattern

## Success Criteria

Fix is complete when:

- ✅ Test passes consistently locally (10+ runs)
- ✅ Test passes consistently on CI/CD (multiple builds)
- ✅ Timing logs show correct operation order
- ✅ No race conditions under load/stress testing
- ✅ Code includes defensive retry logic
- ✅ All async operations properly awaited
- ✅ Status updates cover all transitions
- ✅ Root cause documented in code comments

## Reference

This skill is based on the investigation documented in:
- DELEGATION_PROMPT_ROLLBACK_FIX.md
- Commit: a03ea8f - "fix: restore RollbackBlueGreenDeployment test by fixing race condition"

Key lessons applied:
- Always verify assumptions in code
- Consider environment differences
- Look for async/await patterns carefully
- Apply multiple defensive layers
