using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.IntegrationTests.Helpers;

/// <summary>
/// Builder class for creating test data in integration tests.
/// Provides fluent API for constructing test deployment requests and modules.
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
/// Builder for creating DeploymentRequest objects in tests.
/// </summary>
public class DeploymentRequestBuilder
{
    private readonly DeploymentRequest _request;

    public DeploymentRequestBuilder(string moduleName, string targetEnvironment)
    {
        _request = new DeploymentRequest
        {
            ModuleName = moduleName,
            Version = "1.0.0",
            TargetEnvironment = targetEnvironment,
            RequesterEmail = "integrationtest@example.com",
            ModuleUrl = $"https://example.com/modules/{moduleName}/1.0.0.ko",
            SignatureUrl = $"https://example.com/modules/{moduleName}/1.0.0.sig",
            Hash = "abc123def456", // Mock hash for testing
            VerifySignature = false, // Disable signature verification in tests
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
    /// Sets the module URL.
    /// </summary>
    public DeploymentRequestBuilder WithModuleUrl(string url)
    {
        _request.ModuleUrl = url;
        return this;
    }

    /// <summary>
    /// Enables signature verification for this deployment.
    /// </summary>
    public DeploymentRequestBuilder WithSignatureVerification()
    {
        _request.VerifySignature = true;
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
    /// Sets the hash value.
    /// </summary>
    public DeploymentRequestBuilder WithHash(string hash)
    {
        _request.Hash = hash;
        return this;
    }

    /// <summary>
    /// Builds the DeploymentRequest.
    /// </summary>
    public DeploymentRequest Build()
    {
        return _request;
    }

    /// <summary>
    /// Implicit conversion to DeploymentRequest for convenience.
    /// </summary>
    public static implicit operator DeploymentRequest(DeploymentRequestBuilder builder)
    {
        return builder.Build();
    }
}
