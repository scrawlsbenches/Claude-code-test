using FluentAssertions;
using HotSwap.Distributed.{ProjectName};
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.{ProjectName};

/// <summary>
/// Tests for {ClassName}
///
/// Test naming convention: MethodName_StateUnderTest_ExpectedBehavior
/// Pattern: Arrange-Act-Assert (AAA)
/// Assertions: FluentAssertions for readability
/// </summary>
public class {ClassName}Tests
{
    // ============================================
    // Mocks - Dependencies that need to be mocked
    // ============================================
    private readonly Mock<I{Dependency}> _mock{Dependency};
    private readonly Mock<ILogger<{ClassName}>> _mockLogger;

    // ============================================
    // Test Data - Reusable test objects
    // ============================================
    private readonly {DataModel} _validTestData;

    // ============================================
    // System Under Test (SUT)
    // ============================================
    private readonly {ClassName} _sut;

    /// <summary>
    /// Constructor - Setup runs before EACH test
    /// Initialize mocks and create test data
    /// </summary>
    public {ClassName}Tests()
    {
        // Arrange - Setup mocks
        _mock{Dependency} = new Mock<I{Dependency}>();
        _mockLogger = new Mock<ILogger<{ClassName}>>();

        // Arrange - Create reusable test data
        _validTestData = new {DataModel}
        {
            Id = Guid.NewGuid(),
            Name = "Test Data",
            // Add other properties as needed
        };

        // Arrange - Create System Under Test
        _sut = new {ClassName}(
            _mock{Dependency}.Object,
            _mockLogger.Object
        );
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_ShouldInitialize()
    {
        // Act
        var service = new {ClassName}(_mock{Dependency}.Object, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullDependency_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new {ClassName}(null!, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("{dependency}");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new {ClassName}(_mock{Dependency}.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task {MethodName}Async_WithValidInput_ReturnsExpectedResult()
    {
        // Arrange
        var input = _validTestData;
        var expected = new {ResultType}
        {
            Success = true,
            Data = "expected-data"
        };

        _mock{Dependency}
            .Setup(x => x.{DependencyMethod}Async(
                It.IsAny<{InputType}>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.{MethodName}Async(input, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().Be("expected-data");

        // Verify mock was called correctly
        _mock{Dependency}.Verify(
            x => x.{DependencyMethod}Async(
                It.Is<{InputType}>(i => i.Id == input.Id),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Dependency method should be called exactly once with correct input");
    }

    [Fact]
    public async Task {MethodName}Async_WithMultipleValidInputs_ProcessesAllSuccessfully()
    {
        // Arrange
        var inputs = new List<{InputType}>
        {
            new {InputType} { Id = Guid.NewGuid(), Name = "Input1" },
            new {InputType} { Id = Guid.NewGuid(), Name = "Input2" },
            new {InputType} { Id = Guid.NewGuid(), Name = "Input3" }
        };

        _mock{Dependency}
            .Setup(x => x.{DependencyMethod}Async(
                It.IsAny<{InputType}>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new {ResultType} { Success = true });

        // Act
        var results = new List<{ResultType}>();
        foreach (var input in inputs)
        {
            var result = await _sut.{MethodName}Async(input, CancellationToken.None);
            results.Add(result);
        }

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(r => r.Success == true);

        _mock{Dependency}.Verify(
            x => x.{DependencyMethod}Async(
                It.IsAny<{InputType}>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3),
            "Dependency should be called once per input");
    }

    #endregion

    #region Null/Empty Input Tests

    [Fact]
    public async Task {MethodName}Async_WithNullInput_ThrowsArgumentNullException()
    {
        // Arrange
        {InputType}? input = null;

        // Act
        Func<Task> act = async () => await _sut.{MethodName}Async(input!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("input");

        // Verify dependency was NOT called
        _mock{Dependency}.Verify(
            x => x.{DependencyMethod}Async(
                It.IsAny<{InputType}>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "Dependency should not be called with null input");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task {MethodName}Async_WithEmptyOrWhitespaceString_ThrowsArgumentException(string invalidInput)
    {
        // Arrange
        // (invalidInput from theory)

        // Act
        Func<Task> act = async () => await _sut.{MethodName}Async(invalidInput, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task {MethodName}Async_WhenDependencyThrowsException_PropagatesException()
    {
        // Arrange
        var input = _validTestData;
        var expectedException = new InvalidOperationException("Dependency failed");

        _mock{Dependency}
            .Setup(x => x.{DependencyMethod}Async(
                It.IsAny<{InputType}>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        Func<Task> act = async () => await _sut.{MethodName}Async(input, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Dependency failed");

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Error should be logged");
    }

    [Fact]
    public async Task {MethodName}Async_WhenDependencyReturnsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var input = _validTestData;

        _mock{Dependency}
            .Setup(x => x.{DependencyMethod}Async(
                It.IsAny<{InputType}>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(({ResultType}?)null);

        // Act
        Func<Task> act = async () => await _sut.{MethodName}Async(input, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unexpected null*");
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task {MethodName}Async_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var input = _validTestData;
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        _mock{Dependency}
            .Setup(x => x.{DependencyMethod}Async(
                It.IsAny<{InputType}>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        Func<Task> act = async () => await _sut.{MethodName}Async(input, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task {MethodName}Async_WithMinimumValidInput_ReturnsExpectedResult()
    {
        // Arrange - Minimal valid input
        var input = new {InputType}
        {
            Id = Guid.NewGuid()
            // Only required properties, no optional ones
        };

        _mock{Dependency}
            .Setup(x => x.{DependencyMethod}Async(
                It.IsAny<{InputType}>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new {ResultType} { Success = true });

        // Act
        var result = await _sut.{MethodName}Async(input, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task {MethodName}Async_WithMaximumValidInput_ReturnsExpectedResult()
    {
        // Arrange - All properties populated
        var input = new {InputType}
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Description = "Full description",
            // All optional properties populated
        };

        _mock{Dependency}
            .Setup(x => x.{DependencyMethod}Async(
                It.IsAny<{InputType}>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new {ResultType} { Success = true });

        // Act
        var result = await _sut.{MethodName}Async(input, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Integration/Workflow Tests

    [Fact]
    public async Task CompleteWorkflow_WithValidData_ProcessesSuccessfully()
    {
        // Arrange - Setup entire workflow
        var input = _validTestData;

        _mock{Dependency}
            .Setup(x => x.Step1Async(It.IsAny<{InputType}>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntermediateResult { Data = "step1-data" });

        _mock{Dependency}
            .Setup(x => x.Step2Async(It.IsAny<IntermediateResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new {ResultType} { Success = true });

        // Act - Execute full workflow
        var result = await _sut.ExecuteWorkflowAsync(input, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify workflow steps executed in order
        _mock{Dependency}.Verify(
            x => x.Step1Async(It.IsAny<{InputType}>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mock{Dependency}.Verify(
            x => x.Step2Async(It.IsAny<IntermediateResult>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}

/*
 * USAGE INSTRUCTIONS:
 *
 * 1. Copy this template to your test file
 * 2. Replace placeholders:
 *    - {ProjectName} → Domain, Infrastructure, Orchestrator, or Api
 *    - {ClassName} → The class being tested (e.g., UserAuthenticationService)
 *    - {MethodName} → The method being tested (e.g., AuthenticateAsync)
 *    - {Dependency} → The dependency interface name (e.g., UserRepository)
 *    - {DependencyMethod} → The dependency method name (e.g., GetUserAsync)
 *    - {InputType} → Input parameter type (e.g., AuthenticationRequest)
 *    - {ResultType} → Return type (e.g., AuthenticationResult)
 *    - {DataModel} → Test data model (e.g., User)
 *
 * 3. Remove sections you don't need:
 *    - If no cancellation support → Remove #region Cancellation Tests
 *    - If no error handling → Remove #region Error Handling Tests
 *    - etc.
 *
 * 4. Add/modify test methods as needed for your specific class
 *
 * 5. Follow TDD: Write tests BEFORE implementation
 *    - 🔴 RED: Test fails
 *    - 🟢 GREEN: Implementation passes
 *    - 🔵 REFACTOR: Improve code
 *
 * 6. Run tests: dotnet test --filter "FullyQualifiedName~{ClassName}Tests"
 *
 * 7. Aim for >80% code coverage
 *
 * EXAMPLES:
 *
 * Example 1: Testing UserAuthenticationService
 * - {ClassName} → UserAuthenticationService
 * - {MethodName} → AuthenticateAsync
 * - {Dependency} → UserRepository
 * - {DependencyMethod} → GetUserAsync
 * - {InputType} → string (username), string (password)
 * - {ResultType} → AuthToken
 *
 * Example 2: Testing DeploymentOrchestrator
 * - {ClassName} → DeploymentOrchestrator
 * - {MethodName} → DeployAsync
 * - {Dependency} → DeploymentStrategy
 * - {DependencyMethod} → ExecuteAsync
 * - {InputType} → DeploymentRequest
 * - {ResultType} → DeploymentResult
 */
