using HotSwap.Distributed.Api.Models;

namespace HotSwap.Distributed.IntegrationTests.Helpers;

/// <summary>
/// Builder class for creating test data in integration tests.
/// Provides fluent API for constructing test deployment requests.
/// </summary>
public class TestDataBuilder
{
    /// <summary>
    /// Creates a deployment request builder for the Development environment.
    /// </summary>
    public static DeploymentRequestBuilder ForDevelopment(string moduleName = "test-module")
    {
        return new DeploymentRequestBuilder(moduleName, "Development");
    }

    /// <summary>
    /// Creates a deployment request builder for the QA environment.
    /// </summary>
    public static DeploymentRequestBuilder ForQA(string moduleName = "test-module")
    {
        return new DeploymentRequestBuilder(moduleName, "QA");
    }

    /// <summary>
    /// Creates a deployment request builder for the Staging environment.
    /// </summary>
    public static DeploymentRequestBuilder ForStaging(string moduleName = "test-module")
    {
        return new DeploymentRequestBuilder(moduleName, "Staging");
    }

    /// <summary>
    /// Creates a deployment request builder for the Production environment.
    /// </summary>
    public static DeploymentRequestBuilder ForProduction(string moduleName = "test-module")
    {
        return new DeploymentRequestBuilder(moduleName, "Production");
    }
}

/// <summary>
/// Builder for creating CreateDeploymentRequest objects in tests.
/// </summary>
public class DeploymentRequestBuilder
{
    private readonly CreateDeploymentRequest _request;

    public DeploymentRequestBuilder(string moduleName, string targetEnvironment)
    {
        _request = new CreateDeploymentRequest
        {
            ModuleName = moduleName,
            Version = "1.0.0",
            TargetEnvironment = targetEnvironment,
            RequesterEmail = "integrationtest@example.com",
            RequireApproval = false,
            Description = "Integration test deployment",
            Metadata = new Dictionary<string, string>
            {
                ["IntegrationTest"] = "true",
                ["TestTimestamp"] = DateTime.UtcNow.ToString("O")
            }
        };
    }

    /// <summary>
    /// Sets the module version.
    /// </summary>
    public DeploymentRequestBuilder WithVersion(string version)
    {
        _request.Version = version;
        return this;
    }

    /// <summary>
    /// Sets the requester email.
    /// </summary>
    public DeploymentRequestBuilder WithRequester(string email)
    {
        _request.RequesterEmail = email;
        return this;
    }

    /// <summary>
    /// Sets whether approval is required.
    /// </summary>
    public DeploymentRequestBuilder WithApprovalRequired(bool requireApproval = true)
    {
        _request.RequireApproval = requireApproval;
        return this;
    }

    /// <summary>
    /// Sets the deployment description.
    /// </summary>
    public DeploymentRequestBuilder WithDescription(string description)
    {
        _request.Description = description;
        return this;
    }

    /// <summary>
    /// Adds metadata to the deployment request.
    /// </summary>
    public DeploymentRequestBuilder WithMetadata(string key, string value)
    {
        _request.Metadata ??= new Dictionary<string, string>();
        _request.Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Builds the CreateDeploymentRequest.
    /// </summary>
    public CreateDeploymentRequest Build()
    {
        return _request;
    }

    /// <summary>
    /// Implicit conversion to CreateDeploymentRequest for convenience.
    /// </summary>
    public static implicit operator CreateDeploymentRequest(DeploymentRequestBuilder builder)
    {
        return builder.Build();
    }
}
