# Code Coverage Enforcement

This document describes the automated code coverage enforcement system that ensures all code meets the mandated 85% coverage threshold.

## Overview

The code coverage enforcement system automatically:
- Measures code coverage when tests are run
- Compares coverage against the 85% threshold
- Fails builds if coverage is below the mandated level
- Works in both local development and CI/CD environments
- Provides clear feedback to developers about coverage gaps

## Mandated Coverage Threshold

**Required Coverage**: 85% (line and branch coverage combined)

This threshold is enforced on:
- All pull requests via GitHub Actions
- Local development via the `check-coverage.sh` script
- Pre-release builds

## Quick Start

### Local Development

Run the coverage check script:

```bash
./check-coverage.sh
```

This will:
1. Clean and restore dependencies
2. Build the solution
3. Run all tests with coverage collection
4. Analyze coverage results
5. Display coverage metrics
6. Exit with error if below 85%

### Expected Output

**Success (coverage ≥ 85%):**
```
==========================================
   Code Coverage Enforcement Check
==========================================

Mandated Coverage Threshold: 85%

🧹 Cleaning previous test results...
📦 Restoring dependencies...
🔨 Building solution...
🧪 Running tests with code coverage collection...

✅ Tests passed successfully

📊 Analyzing coverage results...

Coverage Results:
  Line Coverage:   88.45%
  Branch Coverage: 82.30%
  Overall Coverage: 86.12%

==========================================
✅ SUCCESS: Code coverage meets the required threshold!
   Current: 86.12% | Required: 85%
==========================================

Coverage report saved to:
  /home/user/Claude-code-test/TestResults/.../coverage.cobertura.xml
```

**Failure (coverage < 85%):**
```
==========================================
❌ FAILURE: Code coverage is below the required threshold!
   Current: 82.45% | Required: 85%
   Gap: 2.55%
==========================================

⚠️  ACTION REQUIRED:
   1. Add tests to increase coverage
   2. Focus on untested code paths
   3. Review the coverage report for details

Coverage report saved to:
  /home/user/Claude-code-test/TestResults/.../coverage.cobertura.xml
```

## How It Works

### Coverage Collection

The system uses **coverlet.collector** to gather code coverage data during test execution:

```bash
dotnet test --collect:"XPlat Code Coverage" \
    --results-directory TestResults \
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
```

This generates a `coverage.cobertura.xml` file in the TestResults directory.

### Coverage Calculation

Coverage is calculated as a weighted average:
- **Line Coverage** (60% weight): Percentage of code lines executed
- **Branch Coverage** (40% weight): Percentage of decision branches taken

**Overall Coverage** = (Line Coverage × 0.6) + (Branch Coverage × 0.4)

### Threshold Enforcement

The script compares overall coverage to the 85% threshold:
- **≥ 85%**: Script exits with code 0 (success)
- **< 85%**: Script exits with code 1 (failure)

In CI/CD, a non-zero exit code fails the build.

## GitHub Actions Integration

The coverage check is integrated into the `.github/workflows/build-and-test.yml` workflow:

```yaml
code-coverage:
  runs-on: ubuntu-latest
  needs: build-and-test
  timeout-minutes: 10

  steps:
  - uses: actions/checkout@v4

  - name: Setup .NET
    uses: actions/setup-dotnet@v4
    with:
      dotnet-version: '8.0.x'

  - name: Install bc for coverage calculation
    run: sudo apt-get update && sudo apt-get install -y bc

  - name: Check code coverage
    run: |
      chmod +x check-coverage.sh
      ./check-coverage.sh

  - name: Upload coverage report
    if: always()
    uses: actions/upload-artifact@v4
    with:
      name: coverage-report
      path: TestResults/**/coverage.cobertura.xml
      if-no-files-found: warn
```

The `code-coverage` job:
- Runs after the `build-and-test` job completes
- Executes the coverage check script
- Uploads coverage reports as artifacts (available for 90 days)
- Blocks subsequent jobs (integration tests, Docker builds) if coverage is insufficient

## Test Projects with Coverage

All test projects include `coverlet.collector`:

### HotSwap.Distributed.Tests
```xml
<PackageReference Include="coverlet.collector" Version="6.0.0">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

### HotSwap.Distributed.IntegrationTests
Already includes coverlet.collector 6.0.0

### HotSwap.KnowledgeGraph.Tests
Already includes coverlet.collector 6.0.0

## Viewing Detailed Coverage Reports

### Command Line

The script shows summary metrics:
```
Coverage Results:
  Line Coverage:   88.45%
  Branch Coverage: 82.30%
  Overall Coverage: 86.12%
```

### HTML Reports (Optional)

Generate visual HTML reports:

```bash
# Install reportgenerator (one-time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator \
  -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:"TestResults/html" \
  -reporttypes:Html

# Open in browser
open TestResults/html/index.html  # macOS
xdg-open TestResults/html/index.html  # Linux
start TestResults/html/index.html  # Windows
```

The HTML report shows:
- Line-by-line coverage visualization
- Uncovered code highlighted in red
- Branch coverage details
- Coverage by namespace/class/method

## Improving Coverage

### Identify Gaps

1. **Run coverage check:**
   ```bash
   ./check-coverage.sh
   ```

2. **Generate HTML report** to see uncovered lines

3. **Focus on high-impact areas:**
   - Core business logic in `src/HotSwap.Distributed.Orchestrator/`
   - API controllers in `src/HotSwap.Distributed.Api/Controllers/`
   - Infrastructure services in `src/HotSwap.Distributed.Infrastructure/`

### Add Tests

Create tests in `tests/HotSwap.Distributed.Tests/` following TDD:

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var mockDependency = new Mock<IDependency>();
    var sut = new SystemUnderTest(mockDependency.Object);

    // Act
    var result = await sut.MethodAsync(parameters);

    // Assert
    result.Should().NotBeNull();
    result.Property.Should().Be(expectedValue);
}
```

Test categories to improve coverage:
- **Happy paths** - Normal successful execution
- **Edge cases** - Boundary conditions, null/empty inputs
- **Error cases** - Exceptions, invalid input, failures
- **Branch coverage** - All if/else, switch, and ternary paths

## Troubleshooting

### Issue: Coverage file not found

**Error:**
```
❌ ERROR: No coverage file found!
Expected location: TestResults/**/coverage.cobertura.xml
```

**Solutions:**
1. Ensure `coverlet.collector` is installed in test projects
2. Verify test command includes `--collect:"XPlat Code Coverage"`
3. Check that tests actually ran and passed

### Issue: Tests fail

**Error:**
```
❌ Tests failed. Fix failing tests before checking coverage.
```

**Solution:**
Run tests directly to see failures:
```bash
dotnet test --verbosity normal
```

Fix failing tests before running coverage check.

### Issue: Build fails

**Error:**
```
❌ Build failed. Fix build errors before checking coverage.
```

**Solution:**
Run build directly to see errors:
```bash
dotnet build --verbosity normal
```

Fix compilation errors before running coverage check.

### Issue: Script permission denied

**Error:**
```
bash: ./check-coverage.sh: Permission denied
```

**Solution:**
```bash
chmod +x check-coverage.sh
./check-coverage.sh
```

### Issue: bc command not found

**Error:**
```
check-coverage.sh: line 88: bc: command not found
```

**Solution:**
Install bc (basic calculator):
```bash
# Ubuntu/Debian
sudo apt-get install bc

# macOS
brew install bc

# Already included in most Linux distributions
```

## Configuration

### Changing the Threshold

Edit `check-coverage.sh` and modify:

```bash
COVERAGE_THRESHOLD=85  # Change this value
```

Then test:
```bash
./check-coverage.sh
```

**Note:** If you change the threshold, update this documentation and the GitHub Actions workflow accordingly.

### Coverage Calculation Weights

To adjust line vs. branch coverage weights, edit `check-coverage.sh`:

```bash
# Current: 60% line, 40% branch
OVERALL_COVERAGE=$(echo "($LINE_RATE * 0.6 + $BRANCH_RATE * 0.4) * 100" | bc -l | awk '{printf "%.2f", $0}')

# Example: Equal weight (50/50)
OVERALL_COVERAGE=$(echo "($LINE_RATE * 0.5 + $BRANCH_RATE * 0.5) * 100" | bc -l | awk '{printf "%.2f", $0}')
```

## FAQ

**Q: Why 85% coverage?**
A: 85% is a balanced threshold that ensures critical code paths are tested while acknowledging that 100% coverage may be impractical (e.g., defensive error handling, framework code).

**Q: What code is excluded from coverage?**
A: By default, all production code in `src/` is included. Test code in `tests/` and example code in `examples/` are excluded. Generated code and third-party libraries are also excluded.

**Q: Can I run coverage for a single test project?**
A: Yes, but the threshold applies to the entire solution:
```bash
dotnet test tests/HotSwap.Distributed.Tests/ --collect:"XPlat Code Coverage"
```

**Q: How long does the coverage check take?**
A: Typically 1-3 minutes depending on test count and system performance. The script runs a full clean build and all tests with coverage instrumentation.

**Q: What if my PR has 100% coverage but the overall project is below 85%?**
A: The check measures overall solution coverage, not per-PR delta. You may need to add tests for existing code to meet the threshold.

**Q: Can I see coverage trends over time?**
A: GitHub Actions uploads coverage reports as artifacts. You can download and compare them across builds. Consider integrating Codecov or Coveralls for automated trend tracking.

## Best Practices

1. **Run locally before pushing:**
   ```bash
   ./check-coverage.sh && git push
   ```

2. **Add coverage checks to pre-commit hooks:**
   ```bash
   # .git/hooks/pre-commit
   #!/bin/bash
   ./check-coverage.sh || exit 1
   ```

3. **Write tests first (TDD):**
   - Write failing test
   - Implement feature
   - Verify test passes
   - Check coverage increased

4. **Focus on meaningful coverage:**
   - Test business logic thoroughly
   - Don't test framework code
   - Avoid testing trivial getters/setters
   - Test edge cases and error paths

5. **Monitor coverage trends:**
   - Review coverage reports regularly
   - Address declining coverage promptly
   - Celebrate coverage improvements

## Related Documentation

- [CLAUDE.md](CLAUDE.md) - AI assistant guide (includes TDD workflow)
- [TESTING.md](TESTING.md) - Comprehensive testing documentation
- [.github/workflows/build-and-test.yml](.github/workflows/build-and-test.yml) - CI/CD pipeline
- [README.md](README.md) - Project overview

## Changelog

### 2025-11-21
- Initial implementation of code coverage enforcement
- Added `check-coverage.sh` script
- Integrated into GitHub Actions workflow
- Added `coverlet.collector` to HotSwap.Distributed.Tests
- Set mandated threshold at 85%
- Created this documentation

---

**Maintainer:** Claude Code AI Assistant
**Last Updated:** 2025-11-21
**Version:** 1.0.0
