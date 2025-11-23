# Environment Comparison Guide

## How to Find Differences Between Build Server and Development Environment

When tests pass locally but fail/timeout on CI/CD, use this systematic approach to identify differences.

## 1. Gather Environment Information

### Local Environment
```bash
./compare-environments.sh
```

This outputs:
- OS version and kernel
- CPU cores available
- Total memory
- .NET SDK version
- Test execution time with coverage

### CI/CD Environment
Check the GitHub Actions logs for the "Environment diagnostics" step which shows:
- OS version and kernel
- CPU cores available
- Total memory
- .NET SDK version

## 2. Compare Key Metrics

### Hardware Resources
| Metric | Local | CI/CD (GitHub Actions) |
|--------|-------|------------------------|
| CPU Cores | Run `nproc` | Usually 2 cores |
| Memory | Run `free -h` | Usually 7GB |
| Disk I/O | Varies | Shared, variable |

### Test Execution Times
| Phase | Local | CI/CD |
|-------|-------|-------|
| Restore | ? | Check logs |
| Build | ? | Check logs |
| Test Run | ~21s (1,113 tests) | Check logs |
| Coverage | ? | Check logs |

## 3. Identify Bottlenecks

### Common Differences

1. **CPU Performance**
   - Local: Desktop/laptop CPUs (higher single-thread perf)
   - CI/CD: Shared virtualized CPUs (slower)
   - Impact: 2-3x slower test execution

2. **Parallel Execution**
   - Local: May use all CPU cores
   - CI/CD: Limited to 2 cores
   - Check: `dotnet test --help` for parallel options

3. **Coverage Collection Overhead**
   - Adds 20-40% execution time
   - More noticeable on slower hardware

4. **Network/Disk I/O**
   - NuGet restore can be slower on CI/CD
   - Shared disk I/O in virtualized environment

## 4. Diagnostic Steps

### Step 1: Run comparison script locally
```bash
./compare-environments.sh
```
Note the total duration.

### Step 2: Check CI/CD logs
Look for these timestamps in GitHub Actions:
- "Environment diagnostics" output
- "Starting tests at..."
- "Tests completed at..."
- Calculate duration

### Step 3: Compare durations
```
If CI/CD duration > 10 minutes → Need to increase timeout
If CI/CD duration > 3x local → Investigate test performance
If hanging (no completion) → Check for deadlocks/infinite loops
```

## 5. Solutions

### Immediate: Increase Timeouts
```yaml
timeout-minutes: 15  # or higher if needed
```

### Short-term: Optimize Test Performance
- Run tests in parallel: `dotnet test -- -parallel`
- Disable slow tests on CI: Use `[Fact(Skip = "Slow")]`
- Use test categories/traits

### Long-term: Optimize Tests
- Remove Thread.Sleep delays where possible
- Use TestServer instead of full API startup
- Mock expensive I/O operations

## 6. Current Status

### Re-enabled Tests Impact
- Before: 586 tests (33% coverage)
- After: 1,113 tests (expected >35% coverage)
- Increase: +527 tests (91% increase)

### Timeout Adjustments
- Previous: 10 minutes
- Current: 15 minutes
- Reasoning: 91% more tests + coverage overhead

### Expected CI/CD Times
- Restore: ~30 seconds
- Build: ~45 seconds
- Tests: ~90-120 seconds (estimate 3-4x local)
- Coverage: ~30 seconds overhead
- **Total: ~3-4 minutes** (well within 15 min timeout)

## 7. Monitoring

After each push, check:
1. GitHub Actions → Logs → "Environment diagnostics"
2. Compare with local run from `compare-environments.sh`
3. If duration increasing → investigate test additions
4. If timeout → increase limit further or optimize

## 8. Troubleshooting

### Tests timeout on CI/CD but not locally
1. Check for test parallelization differences
2. Look for tests that interact with filesystem
3. Check for race conditions (more likely on slower hardware)
4. Add `--blame-hang-timeout 2m` to identify hanging tests

### Tests fail on CI/CD but pass locally
1. Check .NET version match
2. Look for timezone dependencies
3. Check for hardcoded paths
4. Review environment variable requirements

### Coverage calculation fails
1. Verify coverage.cobertura.xml generated
2. Check `bc` is installed (for calculations)
3. Review check-coverage.sh script output

## Files Created

- `compare-environments.sh` - Run locally to gather metrics
- `.github/workflows/build-and-test.yml` - CI/CD configuration with diagnostics
- This guide - Methodology for comparison
