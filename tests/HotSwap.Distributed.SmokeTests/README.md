# Smoke Tests - Distributed Kernel Orchestration API

Quick smoke tests to verify the API is functioning correctly. These tests are designed to run fast (< 60 seconds) and test critical paths only.

## What are Smoke Tests?

Smoke tests are a subset of test cases that cover the most important functionality of the system. They:
- Run quickly (< 60 seconds total)
- Test critical paths only (health, basic CRUD operations)
- Fail fast on errors
- Are suitable for CI/CD pipelines
- Verify the system is ready for more comprehensive testing

## Test Coverage

The smoke tests verify:

1. **Health Check** - API is up and responding
2. **List Clusters** - GET requests work, all environments present
3. **Get Cluster Info** - GET with parameters works
4. **Create Deployment** - POST requests work
5. **Get Deployment Status** - Deployment tracking works
6. **List Deployments** - Deployment history works

## Running Smoke Tests

### Prerequisites

- .NET 8 SDK installed
- API running (either locally or remote)

### Quick Start

```bash
# Run against localhost:5000 (default)
./run-smoke-tests.sh

# Run against custom URL
./run-smoke-tests.sh http://localhost:5001
./run-smoke-tests.sh https://api.example.com
```

### Manual Run

```bash
# Navigate to smoke tests directory
cd tests/HotSwap.Distributed.SmokeTests

# Build
dotnet build --configuration Release

# Run
dotnet run --configuration Release -- http://localhost:5000
```

## Expected Output

```
╔══════════════════════════════════════════════════════════════╗
║   Distributed Kernel Orchestration API - Smoke Tests        ║
╚══════════════════════════════════════════════════════════════╝

API URL:    http://localhost:5000
Started:    2025-11-15 10:30:00 UTC

Running: Health Check                 ... ✅ PASS
Running: List Clusters                ... ✅ PASS
Running: Get Cluster Info             ... ✅ PASS
Running: Create Deployment            ... ✅ PASS
Running: Get Deployment Status        ... ✅ PASS
Running: List Deployments             ... ✅ PASS

═══════════════════════════════════════════════════════════════
                       TEST RESULTS
═══════════════════════════════════════════════════════════════
✅ Passed:  6
❌ Failed:  0
⏱️  Duration: 8.45s

✅ All smoke tests passed!
```

## CI/CD Integration

Smoke tests are automatically run in GitHub Actions after Docker build:

```yaml
- name: Run smoke tests
  run: ./run-smoke-tests.sh http://localhost:5000
```

See `.github/workflows/build-and-test.yml` for full CI/CD configuration.

## Troubleshooting

### API Not Reachable

**Error:**
```
❌ FAIL: Health Check
Error: Connection refused
```

**Solution:**
- Ensure the API is running
- Check the API URL is correct
- Verify firewall settings

### Tests Timeout

**Error:**
```
❌ FAIL: Create Deployment
Error: Task canceled after 30 seconds
```

**Solution:**
- Check API is not overloaded
- Verify database/Redis are accessible
- Check API logs for errors

### Missing .NET SDK

**Error:**
```
❌ Error: .NET 8 SDK not found
```

**Solution:**
Install .NET 8 SDK:
- Windows: `winget install Microsoft.DotNet.SDK.8`
- Linux: See CLAUDE.md for installation instructions
- macOS: `brew install dotnet@8`

## Comparison with Full Tests

| Aspect | Smoke Tests | Full Unit Tests | Integration Tests |
|--------|-------------|-----------------|-------------------|
| **Purpose** | Verify API is up | Test all logic | Test end-to-end workflows |
| **Duration** | < 60 seconds | 5-10 seconds | 5-30 minutes |
| **Coverage** | Critical paths | All code paths | All workflows |
| **When to Run** | After deploy | Before commit | Before release |
| **Requires API** | ✅ Yes | ❌ No | ✅ Yes |

## Adding New Smoke Tests

To add a new smoke test:

1. Add a test method in `Program.cs`:
   ```csharp
   private static async Task Test_YourNewTest()
   {
       var response = await _httpClient!.GetAsync("/your/endpoint");
       response.EnsureSuccessStatusCode();
       // Add assertions...
   }
   ```

2. Call it from `Main`:
   ```csharp
   await RunTest("Your New Test", Test_YourNewTest);
   ```

3. Run to verify:
   ```bash
   ./run-smoke-tests.sh
   ```

## Best Practices

1. **Keep tests independent** - Each test should work standalone
2. **Fail fast** - Use `EnsureSuccessStatusCode()` and throw on failures
3. **Clean up resources** - Delete test data if needed
4. **Be concise** - Smoke tests should be simple and clear
5. **Test critical paths only** - Don't duplicate unit tests

## Related Documentation

- [TESTING.md](../../TESTING.md) - Complete testing guide
- [CLAUDE.md](../../CLAUDE.md) - Development guidelines
- [examples/ApiUsageExample](../../examples/ApiUsageExample/README.md) - Comprehensive API examples
