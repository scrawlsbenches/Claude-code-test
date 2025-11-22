using FluentAssertions;
using HotSwap.Distributed.Api.Models;
using HotSwap.Distributed.Api.Validation;
using Xunit;

namespace HotSwap.Distributed.Tests.Validation;

public class DeploymentRequestValidatorTests
{
    #region Validate Method Tests

    [Fact]
    public void Validate_WithNullRequest_ShouldReturnFalseWithError()
    {
        // Arrange & Act
        var isValid = DeploymentRequestValidator.Validate(null!, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().ContainSingle();
        errors.Should().Contain("Request body is required");
    }

    [Fact]
    public void Validate_WithValidRequest_ShouldReturnTrue()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    #endregion

    #region ModuleName Validation Tests

    [Fact]
    public void Validate_WithNullModuleName_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ModuleName = null!;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("ModuleName is required");
    }

    [Fact]
    public void Validate_WithEmptyModuleName_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ModuleName = "";

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("ModuleName is required");
    }

    [Fact]
    public void Validate_WithWhitespaceModuleName_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ModuleName = "   ";

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("ModuleName is required");
    }

    [Theory]
    [InlineData("ab")]        // Too short (2 chars)
    [InlineData("a")]         // Too short (1 char)
    public void Validate_WithTooShortModuleName_ShouldReturnError(string moduleName)
    {
        // Arrange
        var request = CreateValidRequest();
        request.ModuleName = moduleName;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("ModuleName must be between 3 and 64 characters");
    }

    [Fact]
    public void Validate_WithTooLongModuleName_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ModuleName = new string('a', 65); // 65 chars

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("ModuleName must be between 3 and 64 characters");
    }

    [Theory]
    [InlineData("UPPERCASE")]      // Uppercase not allowed
    [InlineData("Module-Name")]    // Mixed case not allowed
    [InlineData("-module")]        // Can't start with hyphen
    [InlineData("module-")]        // Can't end with hyphen
    [InlineData("module_name")]    // Underscore not allowed
    [InlineData("module.name")]    // Dot not allowed
    [InlineData("module name")]    // Space not allowed
    [InlineData("module@name")]    // Special char not allowed
    public void Validate_WithInvalidModuleNamePattern_ShouldReturnError(string moduleName)
    {
        // Arrange
        var request = CreateValidRequest();
        request.ModuleName = moduleName;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("ModuleName must contain only lowercase"));
    }

    [Theory]
    [InlineData("abc")]            // Min valid length
    [InlineData("my-module")]      // With hyphen
    [InlineData("my-module-123")]  // With hyphen and numbers
    [InlineData("payment-processor")]
    public void Validate_WithValidModuleName_ShouldPass(string moduleName)
    {
        // Arrange
        var request = CreateValidRequest();
        request.ModuleName = moduleName;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    #endregion

    #region Version Validation Tests

    [Fact]
    public void Validate_WithNullVersion_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Version = null!;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("Version is required");
    }

    [Fact]
    public void Validate_WithEmptyVersion_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Version = "";

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("Version is required");
    }

    [Theory]
    [InlineData("1.0")]           // Missing patch
    [InlineData("1")]             // Only major
    [InlineData("v1.0.0")]        // 'v' prefix not allowed
    [InlineData("1.0.0.0")]       // Too many parts
    [InlineData("1.0.a")]         // Invalid patch
    [InlineData("abc")]           // Non-numeric
    public void Validate_WithInvalidVersionFormat_ShouldReturnError(string version)
    {
        // Arrange
        var request = CreateValidRequest();
        request.Version = version;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("semantic versioning format"));
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("2.5.3")]
    [InlineData("1.0.0-beta")]
    [InlineData("1.0.0-alpha1")]
    [InlineData("10.20.30-rc2")]
    public void Validate_WithValidVersionFormat_ShouldPass(string version)
    {
        // Arrange
        var request = CreateValidRequest();
        request.Version = version;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    #endregion

    #region TargetEnvironment Validation Tests

    [Fact]
    public void Validate_WithNullTargetEnvironment_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.TargetEnvironment = null!;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("TargetEnvironment is required");
    }

    [Fact]
    public void Validate_WithEmptyTargetEnvironment_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.TargetEnvironment = "";

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("TargetEnvironment is required");
    }

    [Theory]
    [InlineData("InvalidEnv")]
    [InlineData("Test")]
    [InlineData("Prod123")]
    public void Validate_WithInvalidTargetEnvironment_ShouldReturnError(string environment)
    {
        // Arrange
        var request = CreateValidRequest();
        request.TargetEnvironment = environment;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("TargetEnvironment must be one of"));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("QA")]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("development")]  // Case-insensitive
    [InlineData("PRODUCTION")]   // Case-insensitive
    public void Validate_WithValidTargetEnvironment_ShouldPass(string environment)
    {
        // Arrange
        var request = CreateValidRequest();
        request.TargetEnvironment = environment;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    #endregion

    #region RequesterEmail Validation Tests

    [Fact]
    public void Validate_WithNullRequesterEmail_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.RequesterEmail = null!;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("RequesterEmail is required");
    }

    [Fact]
    public void Validate_WithEmptyRequesterEmail_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.RequesterEmail = "";

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("RequesterEmail is required");
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user @example.com")]
    [InlineData("user@.com")]
    [InlineData("user@domain")]
    public void Validate_WithInvalidEmailFormat_ShouldReturnError(string email)
    {
        // Arrange
        var request = CreateValidRequest();
        request.RequesterEmail = email;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("valid email address"));
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("john.doe@company.io")]
    [InlineData("admin+test@subdomain.example.com")]
    [InlineData("name_123@test.co.uk")]
    public void Validate_WithValidEmailFormat_ShouldPass(string email)
    {
        // Arrange
        var request = CreateValidRequest();
        request.RequesterEmail = email;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    #endregion

    #region Description Validation Tests

    [Fact]
    public void Validate_WithNullDescription_ShouldPass()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Description = null;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithEmptyDescription_ShouldPass()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Description = "";

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithTooLongDescription_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Description = new string('a', 1001); // 1001 chars

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("Description must not exceed 1000 characters");
    }

    [Fact]
    public void Validate_WithValidDescription_ShouldPass()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Description = "This is a valid description for the deployment.";

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    #endregion

    #region Metadata Validation Tests

    [Fact]
    public void Validate_WithNullMetadata_ShouldPass()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Metadata = null;

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithEmptyMetadata_ShouldPass()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Metadata = new Dictionary<string, string>();

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithTooManyMetadataEntries_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Metadata = new Dictionary<string, string>();
        for (int i = 0; i < 51; i++)
        {
            request.Metadata[$"key{i}"] = $"value{i}";
        }

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("Metadata cannot contain more than 50 entries");
    }

    [Fact]
    public void Validate_WithEmptyMetadataKey_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Metadata = new Dictionary<string, string>
        {
            { "", "value" }
        };

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("Metadata keys cannot be empty");
    }

    [Fact]
    public void Validate_WithWhitespaceMetadataKey_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Metadata = new Dictionary<string, string>
        {
            { "   ", "value" }
        };

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("Metadata keys cannot be empty");
    }

    [Fact]
    public void Validate_WithTooLongMetadataKey_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        var longKey = new string('k', 101);
        request.Metadata = new Dictionary<string, string>
        {
            { longKey, "value" }
        };

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("exceeds 100 characters"));
    }

    [Fact]
    public void Validate_WithTooLongMetadataValue_ShouldReturnError()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Metadata = new Dictionary<string, string>
        {
            { "key", new string('v', 501) }
        };

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("value for key") && e.Contains("exceeds 500 characters"));
    }

    [Fact]
    public void Validate_WithValidMetadata_ShouldPass()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Metadata = new Dictionary<string, string>
        {
            { "team", "platform" },
            { "project", "api-gateway" },
            { "cost-center", "12345" }
        };

        // Act
        var isValid = DeploymentRequestValidator.Validate(request, out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    #endregion

    #region ValidateAndThrow Tests

    [Fact]
    public void ValidateAndThrow_WithValidRequest_ShouldNotThrow()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var act = () => DeploymentRequestValidator.ValidateAndThrow(request);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateAndThrow_WithInvalidRequest_ShouldThrowValidationException()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ModuleName = "";
        request.Version = "invalid";

        // Act
        var act = () => DeploymentRequestValidator.ValidateAndThrow(request);

        // Assert
        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void ValidateAndThrow_WithMultipleErrors_ShouldIncludeAllErrors()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ModuleName = "";
        request.Version = "";
        request.RequesterEmail = "invalid";

        // Act
        var act = () => DeploymentRequestValidator.ValidateAndThrow(request);

        // Assert
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Errors.Should().HaveCount(3);
        exception.Message.Should().Contain("ModuleName");
        exception.Message.Should().Contain("Version");
        exception.Message.Should().Contain("RequesterEmail");
    }

    #endregion

    #region Helper Methods

    private CreateDeploymentRequest CreateValidRequest()
    {
        return new CreateDeploymentRequest
        {
            ModuleName = "payment-module",
            Version = "1.0.0",
            TargetEnvironment = "Development",
            RequesterEmail = "user@example.com",
            RequireApproval = false,
            Description = "Test deployment"
        };
    }

    #endregion
}
