using System.Diagnostics;
using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Telemetry;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

/// <summary>
/// Unit tests for TelemetryProvider.
/// </summary>
public class TelemetryProviderTests : IDisposable
{
    private readonly TelemetryProvider _provider;

    public TelemetryProviderTests()
    {
        _provider = new TelemetryProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    [Fact]
    public void Constructor_InitializesSuccessfully()
    {
        // Arrange & Act
        using var provider = new TelemetryProvider();

        // Assert
        provider.Should().NotBeNull();
    }

    [Fact]
    public void ServiceName_ReturnsExpectedValue()
    {
        // Assert
        TelemetryProvider.ServiceName.Should().Be("HotSwap.DistributedKernel");
    }

    [Fact]
    public void ServiceVersion_ReturnsExpectedValue()
    {
        // Assert
        TelemetryProvider.ServiceVersion.Should().Be("1.0.0");
    }

    [Fact]
    public void StartDeploymentActivity_WithValidParameters_DoesNotThrow()
    {
        // Arrange
        var moduleName = "TestModule";
        var version = new Version(1, 2, 3);
        var environment = EnvironmentType.Production;
        var strategy = "BlueGreen";

        // Act
        Action act = () => _provider.StartDeploymentActivity(moduleName, version, environment, strategy);

        // Assert - Activity may be null if no listener is registered (normal in unit tests)
        act.Should().NotThrow();
    }

    [Fact]
    public void StartNodeActivity_WithValidParameters_DoesNotThrow()
    {
        // Arrange
        var operationName = "node.update";
        var nodeId = Guid.NewGuid();
        var hostname = "node-01.example.com";

        // Act
        Action act = () => _provider.StartNodeActivity(operationName, nodeId, hostname);

        // Assert - Activity may be null if no listener is registered (normal in unit tests)
        act.Should().NotThrow();
    }

    [Fact]
    public void StartStageActivity_WithStageNameOnly_DoesNotThrow()
    {
        // Arrange
        var stageName = "validation";

        // Act
        Action act = () => _provider.StartStageActivity(stageName);

        // Assert - Activity may be null if no listener is registered (normal in unit tests)
        act.Should().NotThrow();
    }

    [Fact]
    public void StartStageActivity_WithEnvironment_DoesNotThrow()
    {
        // Arrange
        var stageName = "deployment";
        var environment = EnvironmentType.Staging;

        // Act
        Action act = () => _provider.StartStageActivity(stageName, environment);

        // Assert - Activity may be null if no listener is registered (normal in unit tests)
        act.Should().NotThrow();
    }

    [Fact]
    public void StartHealthCheckActivity_WithNodeId_DoesNotThrow()
    {
        // Arrange
        var nodeId = Guid.NewGuid();

        // Act
        Action act = () => _provider.StartHealthCheckActivity(nodeId);

        // Assert - Activity may be null if no listener is registered (normal in unit tests)
        act.Should().NotThrow();
    }

    [Fact]
    public void StartRollbackActivity_WithValidParameters_DoesNotThrow()
    {
        // Arrange
        var moduleName = "FailedModule";
        var environment = EnvironmentType.Production;

        // Act
        Action act = () => _provider.StartRollbackActivity(moduleName, environment);

        // Assert - Activity may be null if no listener is registered (normal in unit tests)
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordDeploymentSuccess_WithValidResult_DoesNotThrow()
    {
        // Arrange
        var result = new DeploymentResult
        {
            Strategy = "BlueGreen",
            Environment = EnvironmentType.Production,
            Success = true,
            Message = "Deployment successful",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow,
            NodeResults = new List<NodeDeploymentResult>
            {
                new() { NodeId = Guid.NewGuid(), Success = true },
                new() { NodeId = Guid.NewGuid(), Success = true },
                new() { NodeId = Guid.NewGuid(), Success = true }
            }
        };

        var activity = _provider.StartDeploymentActivity("TestModule", new Version(1, 0, 0), EnvironmentType.Production, "BlueGreen");

        // Act
        Action act = () => _provider.RecordDeploymentSuccess(activity, result);

        // Assert
        act.Should().NotThrow();

        activity?.Dispose();
    }

    [Fact]
    public void RecordDeploymentSuccess_WithNullActivity_DoesNotThrow()
    {
        // Arrange
        var result = new DeploymentResult
        {
            Strategy = "Canary",
            Environment = EnvironmentType.Staging,
            Success = true,
            StartTime = DateTime.UtcNow.AddMinutes(-1),
            EndTime = DateTime.UtcNow,
            NodeResults = new List<NodeDeploymentResult>()
        };

        // Act
        Action act = () => _provider.RecordDeploymentSuccess(null, result);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordDeploymentFailure_WithValidResult_DoesNotThrow()
    {
        // Arrange
        var exception = new InvalidOperationException("Deployment validation failed");
        var result = new DeploymentResult
        {
            Strategy = "AllAtOnce",
            Environment = EnvironmentType.Development,
            Success = false,
            Message = "Deployment failed",
            Exception = exception,
            StartTime = DateTime.UtcNow.AddMinutes(-2),
            EndTime = DateTime.UtcNow,
            NodeResults = new List<NodeDeploymentResult>()
        };

        var activity = _provider.StartDeploymentActivity("FailingModule", new Version(1, 0, 0), EnvironmentType.Development, "AllAtOnce");

        // Act
        Action act = () => _provider.RecordDeploymentFailure(activity, result, exception);

        // Assert
        act.Should().NotThrow();

        activity?.Dispose();
    }

    [Fact]
    public void RecordDeploymentFailure_WithNullException_DoesNotThrow()
    {
        // Arrange
        var result = new DeploymentResult
        {
            Strategy = "RollingUpdate",
            Environment = EnvironmentType.Production,
            Success = false,
            Message = "Unknown failure",
            StartTime = DateTime.UtcNow.AddMinutes(-1),
            EndTime = DateTime.UtcNow,
            NodeResults = new List<NodeDeploymentResult>()
        };

        var activity = _provider.StartDeploymentActivity("Module", new Version(1, 0, 0), EnvironmentType.Production, "RollingUpdate");

        // Act
        Action act = () => _provider.RecordDeploymentFailure(activity, result, null);

        // Assert
        act.Should().NotThrow();

        activity?.Dispose();
    }

    [Fact]
    public void RecordDeploymentFailure_WithNullActivity_DoesNotThrow()
    {
        // Arrange
        var result = new DeploymentResult
        {
            Strategy = "Canary",
            Environment = EnvironmentType.Staging,
            Success = false,
            Message = "Failed",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow,
            NodeResults = new List<NodeDeploymentResult>()
        };

        // Act
        Action act = () => _provider.RecordDeploymentFailure(null, result, null);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordRollback_WithSuccessfulRollback_DoesNotThrow()
    {
        // Arrange
        var moduleName = "RollbackModule";
        var environment = EnvironmentType.Production;
        var nodesAffected = 5;
        var activity = _provider.StartRollbackActivity(moduleName, environment);

        // Act
        Action act = () => _provider.RecordRollback(activity, moduleName, environment, nodesAffected, success: true);

        // Assert
        act.Should().NotThrow();

        activity?.Dispose();
    }

    [Fact]
    public void RecordRollback_WithFailedRollback_DoesNotThrow()
    {
        // Arrange
        var moduleName = "FailedRollback";
        var environment = EnvironmentType.Staging;
        var nodesAffected = 2;
        var activity = _provider.StartRollbackActivity(moduleName, environment);

        // Act
        Action act = () => _provider.RecordRollback(activity, moduleName, environment, nodesAffected, success: false);

        // Assert
        act.Should().NotThrow();

        activity?.Dispose();
    }

    [Fact]
    public void RecordRollback_WithNullActivity_DoesNotThrow()
    {
        // Arrange
        var moduleName = "Module";
        var environment = EnvironmentType.Development;
        var nodesAffected = 1;

        // Act
        Action act = () => _provider.RecordRollback(null, moduleName, environment, nodesAffected, success: true);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordHealthCheck_WithHealthyNode_DoesNotThrow()
    {
        // Arrange
        var nodeId = Guid.NewGuid();
        var duration = TimeSpan.FromMilliseconds(50);
        var activity = _provider.StartHealthCheckActivity(nodeId);

        // Act
        Action act = () => _provider.RecordHealthCheck(activity, duration, healthy: true);

        // Assert
        act.Should().NotThrow();

        activity?.Dispose();
    }

    [Fact]
    public void RecordHealthCheck_WithUnhealthyNode_DoesNotThrow()
    {
        // Arrange
        var nodeId = Guid.NewGuid();
        var duration = TimeSpan.FromSeconds(5);
        var activity = _provider.StartHealthCheckActivity(nodeId);

        // Act
        Action act = () => _provider.RecordHealthCheck(activity, duration, healthy: false);

        // Assert
        act.Should().NotThrow();

        activity?.Dispose();
    }

    [Fact]
    public void RecordHealthCheck_WithNullActivity_DoesNotThrow()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        Action act = () => _provider.RecordHealthCheck(null, duration, healthy: true);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ExtractTraceContext_WithValidTraceparent_ReturnsContext()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            { "traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" }
        };

        // Act
        var context = _provider.ExtractTraceContext(headers);

        // Assert
        context.Should().NotBeNull();
        context!.Value.TraceId.Should().NotBe(ActivityTraceId.CreateRandom());
    }

    [Fact]
    public void ExtractTraceContext_WithoutTraceparent_ReturnsNull()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            { "some-header", "some-value" }
        };

        // Act
        var context = _provider.ExtractTraceContext(headers);

        // Assert
        context.Should().BeNull();
    }

    [Fact]
    public void ExtractTraceContext_WithInvalidTraceparent_ReturnsNull()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            { "traceparent", "invalid-traceparent-format" }
        };

        // Act
        var context = _provider.ExtractTraceContext(headers);

        // Assert
        context.Should().BeNull();
    }

    [Fact]
    public void ExtractTraceContext_WithEmptyDictionary_ReturnsNull()
    {
        // Arrange
        var headers = new Dictionary<string, string>();

        // Act
        var context = _provider.ExtractTraceContext(headers);

        // Assert
        context.Should().BeNull();
    }

    [Fact]
    public void InjectTraceContext_WithValidActivity_DoesNotThrow()
    {
        // Arrange
        var headers = new Dictionary<string, string>();
        var activity = _provider.StartDeploymentActivity("Module", new Version(1, 0, 0), EnvironmentType.Production, "BlueGreen");

        // Act
        Action act = () => _provider.InjectTraceContext(activity, headers);

        // Assert - Activity may be null if no listener registered, which is expected in unit tests
        act.Should().NotThrow();

        activity?.Dispose();
    }

    [Fact]
    public void InjectTraceContext_WithNullActivity_DoesNotModifyHeaders()
    {
        // Arrange
        var headers = new Dictionary<string, string>();

        // Act
        _provider.InjectTraceContext(null, headers);

        // Assert
        headers.Should().BeEmpty();
    }

    [Fact]
    public void InjectTraceContext_WithActivityWithTraceState_DoesNotThrow()
    {
        // Arrange
        var headers = new Dictionary<string, string>();
        var activity = _provider.StartDeploymentActivity("Module", new Version(1, 0, 0), EnvironmentType.Production, "BlueGreen");

        if (activity != null)
        {
            activity.TraceStateString = "vendor1=value1";
        }

        // Act
        Action act = () => _provider.InjectTraceContext(activity, headers);

        // Assert - Activity may be null if no listener registered, which is expected in unit tests
        act.Should().NotThrow();

        activity?.Dispose();
    }

    [Fact]
    public void SetBaggage_WithValidKeyValue_DoesNotThrow()
    {
        // Arrange
        var key = "user.id";
        var value = "12345";

        // Act
        Action act = () => _provider.SetBaggage(key, value);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GetBaggage_WithNonExistentKey_ReturnsNull()
    {
        // Arrange
        var key = "nonexistent.key";

        // Act
        var value = _provider.GetBaggage(key);

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void SetBaggage_ThenGetBaggage_DoesNotThrow()
    {
        // Arrange
        var key = "deployment.id";
        var value = "deploy-12345";
        var activity = _provider.StartDeploymentActivity("Module", new Version(1, 0, 0), EnvironmentType.Production, "BlueGreen");

        // Act
        Action act = () =>
        {
            _provider.SetBaggage(key, value);
            _provider.GetBaggage(key);
        };

        // Assert - Baggage requires active activity context, may be null if no listener registered
        act.Should().NotThrow();

        activity?.Dispose();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var provider = new TelemetryProvider();

        // Act
        Action act = () =>
        {
            provider.Dispose();
            provider.Dispose();
            provider.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void CompleteWorkflow_CreatesAndRecordsDeployment_DoesNotThrow()
    {
        // Arrange
        var moduleName = "WorkflowModule";
        var version = new Version(2, 0, 0);
        var environment = EnvironmentType.Production;
        var strategy = "BlueGreen";

        var result = new DeploymentResult
        {
            Strategy = strategy,
            Environment = environment,
            Success = true,
            Message = "Deployment successful",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow,
            NodeResults = new List<NodeDeploymentResult>
            {
                new() { NodeId = Guid.NewGuid(), Success = true }
            }
        };

        // Act
        Action act = () =>
        {
            var activity = _provider.StartDeploymentActivity(moduleName, version, environment, strategy);
            _provider.RecordDeploymentSuccess(activity, result);
            activity?.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void FailedWorkflow_CreatesAndRecordsFailure_DoesNotThrow()
    {
        // Arrange
        var moduleName = "FailedModule";
        var version = new Version(1, 5, 0);
        var environment = EnvironmentType.Staging;
        var strategy = "Canary";
        var exception = new Exception("Validation failed");

        var result = new DeploymentResult
        {
            Strategy = strategy,
            Environment = environment,
            Success = false,
            Message = "Deployment failed",
            Exception = exception,
            StartTime = DateTime.UtcNow.AddMinutes(-2),
            EndTime = DateTime.UtcNow,
            NodeResults = new List<NodeDeploymentResult>()
        };

        // Act
        Action act = () =>
        {
            var activity = _provider.StartDeploymentActivity(moduleName, version, environment, strategy);
            _provider.RecordDeploymentFailure(activity, result, exception);
            activity?.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RollbackWorkflow_CreatesAndRecordsRollback_DoesNotThrow()
    {
        // Arrange
        var moduleName = "RollbackModule";
        var environment = EnvironmentType.Production;
        var nodesAffected = 3;

        // Act
        Action act = () =>
        {
            var activity = _provider.StartRollbackActivity(moduleName, environment);
            _provider.RecordRollback(activity, moduleName, environment, nodesAffected, success: true);
            activity?.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void HealthCheckWorkflow_CreatesAndRecordsHealthCheck_DoesNotThrow()
    {
        // Arrange
        var nodeId = Guid.NewGuid();
        var duration = TimeSpan.FromMilliseconds(75);

        // Act
        Action act = () =>
        {
            var activity = _provider.StartHealthCheckActivity(nodeId);
            _provider.RecordHealthCheck(activity, duration, healthy: true);
            activity?.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void TraceContextPropagation_InjectAndExtract_DoesNotThrow()
    {
        // Arrange
        var outgoingHeaders = new Dictionary<string, string>();
        var activity = _provider.StartDeploymentActivity("Module", new Version(1, 0, 0), EnvironmentType.Production, "BlueGreen");

        // Act
        Action act = () =>
        {
            _provider.InjectTraceContext(activity, outgoingHeaders);
            _provider.ExtractTraceContext(outgoingHeaders);
        };

        // Assert - Activity may be null if no listener registered, which is expected in unit tests
        act.Should().NotThrow();

        activity?.Dispose();
    }
}
