# Test-Driven Development (TDD) Helper Skill

**Description**: Guides you through the mandatory Red-Green-Refactor cycle for test-driven development in this .NET project.

**When to use**:
- Starting any new feature implementation
- Fixing bugs
- Refactoring existing code
- Whenever you need to write code (TDD is MANDATORY)

## Instructions

This skill enforces the Test-Driven Development workflow that is MANDATORY for all code changes in this project. Follow these steps strictly.

---

## The Red-Green-Refactor Cycle

```
🔴 RED → 🟢 GREEN → 🔵 REFACTOR
```

### Phase 1: 🔴 RED - Write Failing Test

**CRITICAL**: You MUST write the test BEFORE writing implementation code.

#### Step 1.1: Identify What to Test

Ask yourself:
- What behavior am I implementing?
- What should happen with valid input?
- What should happen with invalid input?
- What edge cases exist?

#### Step 1.2: Write the Test

```bash
# Navigate to test project
cd tests/HotSwap.Distributed.Tests/

# Create or edit test file
# Example: UserAuthenticationTests.cs
```

**Test structure (AAA Pattern)**:
```csharp
[Fact]
public async Task MethodName_StateUnderTest_ExpectedBehavior()
{
    // Arrange - Set up test data and mocks
    var mockDependency = new Mock<IDependency>();
    mockDependency.Setup(x => x.MethodAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(expectedValue);

    var sut = new SystemUnderTest(mockDependency.Object);

    // Act - Execute the method being tested
    var result = await sut.MethodUnderTestAsync(input);

    // Assert - Verify expected behavior using FluentAssertions
    result.Should().NotBeNull();
    result.Property.Should().Be(expectedValue);
}
```

#### Step 1.3: Run the Test - It MUST Fail

```bash
# Run the specific test
dotnet test --filter "FullyQualifiedName~MethodName_StateUnderTest"
```

**Expected output**:
```
Test Failed
[Compiler error or assertion failure]
```

**✅ GOOD**: Test fails (RED phase complete)
**❌ BAD**: Test passes without implementation (test is wrong - fix the test!)

---

### Phase 2: 🟢 GREEN - Make Test Pass

**Goal**: Write the MINIMUM code to make the test pass.

#### Step 2.1: Implement the Feature

```bash
# Navigate to source project
cd src/HotSwap.Distributed.[ProjectName]/

# Create or edit implementation file
```

**Write ONLY enough code to pass the test**:
```csharp
public class SystemUnderTest
{
    private readonly IDependency _dependency;

    public SystemUnderTest(IDependency dependency)
    {
        _dependency = dependency ?? throw new ArgumentNullException(nameof(dependency));
    }

    public async Task<Result> MethodUnderTestAsync(Input input)
    {
        // Minimal implementation to pass test
        var value = await _dependency.MethodAsync(input.Value, CancellationToken.None);
        return new Result { Property = value };
    }
}
```

#### Step 2.2: Run the Test Again - It MUST Pass

```bash
# Run the specific test
dotnet test --filter "FullyQualifiedName~MethodName_StateUnderTest"
```

**Expected output**:
```
Test Passed
```

**✅ GOOD**: Test passes (GREEN phase complete)
**❌ BAD**: Test still fails (keep implementing until it passes)

---

### Phase 3: 🔵 REFACTOR - Improve Code Quality

**Goal**: Improve code quality while keeping ALL tests green.

#### Step 3.1: Identify Improvements

Look for:
- Code duplication (DRY principle)
- Long methods (extract helper methods)
- Poor naming (improve clarity)
- Missing error handling
- Performance issues
- SOLID principle violations

#### Step 3.2: Refactor Code

**Example refactorings**:

1. **Extract method**:
```csharp
// Before
public async Task<Result> ProcessAsync(Input input)
{
    // 50 lines of code...
}

// After
public async Task<Result> ProcessAsync(Input input)
{
    ValidateInput(input);
    var data = await FetchDataAsync(input);
    return TransformData(data);
}
```

2. **Add error handling**:
```csharp
public async Task<Result> MethodAsync(Input input)
{
    if (input == null)
        throw new ArgumentNullException(nameof(input));

    if (string.IsNullOrWhiteSpace(input.Value))
        throw new ArgumentException("Value cannot be empty", nameof(input));

    // Implementation...
}
```

3. **Add XML documentation**:
```csharp
/// <summary>
/// Processes the input and returns a result.
/// </summary>
/// <param name="input">The input to process.</param>
/// <returns>A result containing the processed data.</returns>
/// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
public async Task<Result> MethodAsync(Input input)
{
    // Implementation...
}
```

#### Step 3.3: Run ALL Tests After Each Refactoring

**CRITICAL**: Tests MUST stay green during refactoring.

```bash
# Run all tests after EACH refactoring
dotnet test
```

**Expected output**:
```
Passed!  - Failed:     0, Passed:   568, Skipped:    14, Total:   582
```

**If ANY test fails**:
- ❌ Revert your refactoring
- ❌ Try a smaller refactoring step
- ❌ Fix the issue before proceeding

---

## Phase 4: 🔄 Repeat for Additional Test Cases

Once the first test passes and code is refactored, repeat the cycle for:

1. **Edge cases**:
   - Empty input
   - Null values
   - Boundary conditions
   - Maximum/minimum values

2. **Error cases**:
   - Invalid input
   - Exceptions from dependencies
   - Timeout scenarios
   - Concurrent access

3. **Alternative scenarios**:
   - Different valid inputs
   - Different execution paths
   - Different outcomes

**For each scenario**: 🔴 RED → 🟢 GREEN → 🔵 REFACTOR

---

## Test Quality Checklist

Before considering the feature complete, verify:

### Test Structure
- ✅ Test name follows `MethodName_StateUnderTest_ExpectedBehavior`
- ✅ Uses AAA pattern (Arrange-Act-Assert)
- ✅ One logical assertion per test (may have multiple Should() calls)
- ✅ Test is independent (doesn't depend on other tests)

### Test Coverage
- ✅ Happy path covered (normal successful execution)
- ✅ Edge cases covered (boundaries, empty, null)
- ✅ Error cases covered (exceptions, failures)
- ✅ Async patterns covered (cancellation, timeouts if applicable)

### Assertions
- ✅ Uses FluentAssertions (`.Should()` syntax)
- ✅ Assertions are specific (checks exact values, not just not-null)
- ✅ Error messages are clear if test fails

### Mocks
- ✅ Mock setups match actual method signatures
- ✅ All required dependencies are mocked
- ✅ Mock verifies behavior if needed (`.Verify()`)

---

## Common TDD Mistakes to Avoid

### ❌ WRONG: Implementation Before Test
```bash
# Don't do this:
1. Write implementation code
2. Write test (maybe)
3. Run test
4. Commit
```

### ✅ CORRECT: Test Before Implementation
```bash
# Always do this:
1. 🔴 Write failing test
2. 🟢 Write minimal implementation
3. 🔵 Refactor for quality
4. Run all tests
5. Commit (only if all tests pass)
```

### ❌ WRONG: Testing Implementation Details
```csharp
// Don't test private methods directly
[Fact]
public void PrivateHelperMethod_Works()
{
    // This couples test to implementation
}
```

### ✅ CORRECT: Testing Public Behavior
```csharp
// Test public API, private methods are tested indirectly
[Fact]
public async Task PublicMethod_WithValidInput_ReturnsExpectedResult()
{
    // Test behavior, not implementation
}
```

### ❌ WRONG: Skipping Refactor Phase
```csharp
// Don't leave code messy after making test pass
public async Task Method(Input i) // Poor naming
{
    var x = await _d.Get(i.V); // Cryptic variables
    return new Result { P = x }; // No error handling
}
```

### ✅ CORRECT: Refactor for Quality
```csharp
/// <summary>
/// Processes the input and retrieves associated data.
/// </summary>
public async Task<Result> ProcessInputAsync(Input input)
{
    ValidateInput(input);

    var data = await _dataRepository.GetAsync(input.Value, CancellationToken.None);

    return new Result { Property = data };
}
```

---

## Integration with Pre-Commit Checklist

**Before committing TDD work**:

1. Run pre-commit checklist:
```bash
dotnet clean && dotnet restore && dotnet build --no-incremental && dotnet test
```

2. Verify all phases complete:
   - ✅ 🔴 RED: Tests were written first and failed initially
   - ✅ 🟢 GREEN: Implementation makes all tests pass
   - ✅ 🔵 REFACTOR: Code quality is high
   - ✅ ALL tests pass (zero failures)

3. Only THEN commit:
```bash
git add .
git commit -m "feat: add feature X (TDD: 5 tests added)"
```

---

## Quick Reference: TDD Workflow

```bash
# 1. 🔴 RED - Write failing test
cd tests/HotSwap.Distributed.Tests/
# Edit test file...
dotnet test --filter "FullyQualifiedName~YourTest"
# Expected: Test FAILS

# 2. 🟢 GREEN - Make test pass
cd src/HotSwap.Distributed.[Project]/
# Edit implementation file...
dotnet test --filter "FullyQualifiedName~YourTest"
# Expected: Test PASSES

# 3. 🔵 REFACTOR - Improve quality
# Edit implementation to improve code quality...
dotnet test  # Run ALL tests
# Expected: ALL tests still PASS

# 4. 🔄 Repeat for next test case
```

---

## Success Criteria

TDD is complete when:

- ✅ All tests were written BEFORE implementation
- ✅ Tests initially failed (RED)
- ✅ Implementation made tests pass (GREEN)
- ✅ Code was refactored for quality (REFACTOR)
- ✅ ALL tests pass (zero failures)
- ✅ Test coverage includes happy path, edge cases, error cases
- ✅ Code follows SOLID principles
- ✅ XML documentation added for public APIs
- ✅ Pre-commit checklist passes

---

## Resources

**CLAUDE.md References**:
- Test-Driven Development (TDD) Workflow (line 642)
- Testing Requirements (line 1057)
- Pre-Commit Checklist (line 561)

**Key Principle**:
> "MANDATORY: All coding tasks MUST follow Test-Driven Development (TDD) principles. This is not optional."

---

## Performance Notes

- Writing test first: ~5-10 minutes
- Minimal implementation: ~5-15 minutes
- Refactoring: ~5-10 minutes
- Total per feature: ~15-35 minutes
- **Benefit**: Catches bugs during development, not in CI/CD

## Automation

Use this skill at the start of any coding task:
```
/tdd-helper
```

The skill will guide you through the Red-Green-Refactor cycle step by step.
