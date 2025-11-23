# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.ServiceMesh.Domain.Models`

---

## Table of Contents

1. [Policy](#policy)
2. [PolicySpec](#policyspec)
3. [Deployment](#deployment)
4. [ValidationResult](#validationresult)
5. [ServiceMeshCluster](#servicemeshcluster)
6. [Enumerations](#enumerations)
7. [Value Objects](#value-objects)

---

## Policy

Represents a service mesh policy configuration.

**File:** `src/HotSwap.ServiceMesh.Domain/Models/Policy.cs`

```csharp
namespace HotSwap.ServiceMesh.Domain.Models;

/// <summary>
/// Represents a service mesh policy.
/// </summary>
public class Policy
{
    /// <summary>
    /// Unique policy identifier (GUID format).
    /// </summary>
    public required string PolicyId { get; set; }

    /// <summary>
    /// Policy name (alphanumeric, dots, dashes allowed).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Policy type (VirtualService, DestinationRule, etc.).
    /// </summary>
    public required PolicyType Type { get; set; }

    /// <summary>
    /// Target service mesh (Istio or Linkerd).
    /// </summary>
    public required ServiceMeshType ServiceMesh { get; set; }

    /// <summary>
    /// Target service name (e.g., "user-service").
    /// </summary>
    public required string TargetService { get; set; }

    /// <summary>
    /// Target namespace (e.g., "default", "production").
    /// </summary>
    public string Namespace { get; set; } = "default";

    /// <summary>
    /// Policy specification (YAML format).
    /// </summary>
    public required PolicySpec Spec { get; set; }

    /// <summary>
    /// Policy version number (incremented on each update).
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Current policy status.
    /// </summary>
    public PolicyStatus Status { get; set; } = PolicyStatus.Draft;

    /// <summary>
    /// Admin user who approved the policy (if approved).
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Policy creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Policy owner/creator user ID.
    /// </summary>
    public required string OwnerId { get; set; }

    /// <summary>
    /// Policy tags for organization (e.g., "circuit-breaker", "production").
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Validates the policy configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(PolicyId))
            errors.Add("PolicyId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Name, @"^[a-zA-Z0-9._-]+$"))
            errors.Add("Name must contain only alphanumeric characters, dots, dashes, and underscores");

        if (string.IsNullOrWhiteSpace(TargetService))
            errors.Add("TargetService is required");

        if (string.IsNullOrWhiteSpace(Namespace))
            errors.Add("Namespace is required");

        if (Spec == null)
            errors.Add("Spec is required");
        else if (!Spec.IsValid(out var specErrors))
            errors.AddRange(specErrors);

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if the policy is approved for production use.
    /// </summary>
    public bool IsApproved() => Status == PolicyStatus.Approved;

    /// <summary>
    /// Creates a new version of this policy.
    /// </summary>
    public Policy CreateNewVersion()
    {
        return new Policy
        {
            PolicyId = Guid.NewGuid().ToString(),
            Name = Name,
            Description = Description,
            Type = Type,
            ServiceMesh = ServiceMesh,
            TargetService = TargetService,
            Namespace = Namespace,
            Spec = Spec.Clone(),
            Version = Version + 1,
            Status = PolicyStatus.Draft,
            OwnerId = OwnerId,
            Tags = new List<string>(Tags)
        };
    }
}
```

---

## PolicySpec

Represents the policy specification (configuration).

**File:** `src/HotSwap.ServiceMesh.Domain/Models/PolicySpec.cs`

```csharp
namespace HotSwap.ServiceMesh.Domain.Models;

/// <summary>
/// Represents a policy specification.
/// </summary>
public class PolicySpec
{
    /// <summary>
    /// YAML configuration for the policy.
    /// </summary>
    public required string YamlConfig { get; set; }

    /// <summary>
    /// Parsed configuration as JSON (for validation/querying).
    /// </summary>
    public string? JsonConfig { get; set; }

    /// <summary>
    /// Configuration parameters (key-value pairs).
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>
    /// Validates the policy specification.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(YamlConfig))
        {
            errors.Add("YamlConfig is required");
            return false;
        }

        // Validate YAML syntax
        try
        {
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
            deserializer.Deserialize<object>(YamlConfig);
        }
        catch (Exception ex)
        {
            errors.Add($"Invalid YAML syntax: {ex.Message}");
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Creates a deep clone of this specification.
    /// </summary>
    public PolicySpec Clone()
    {
        return new PolicySpec
        {
            YamlConfig = YamlConfig,
            JsonConfig = JsonConfig,
            Parameters = new Dictionary<string, string>(Parameters)
        };
    }
}
```

---

## Deployment

Represents a policy deployment.

**File:** `src/HotSwap.ServiceMesh.Domain/Models/Deployment.cs`

```csharp
namespace HotSwap.ServiceMesh.Domain.Models;

/// <summary>
/// Represents a policy deployment.
/// </summary>
public class Deployment
{
    /// <summary>
    /// Unique deployment identifier (GUID format).
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Policy being deployed.
    /// </summary>
    public required string PolicyId { get; set; }

    /// <summary>
    /// Target environment (Dev, Staging, Production).
    /// </summary>
    public required string Environment { get; set; }

    /// <summary>
    /// Target cluster ID.
    /// </summary>
    public required string ClusterId { get; set; }

    /// <summary>
    /// Deployment strategy.
    /// </summary>
    public required DeploymentStrategy Strategy { get; set; }

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;

    /// <summary>
    /// Canary percentage (for canary deployments).
    /// </summary>
    public int? CanaryPercentage { get; set; }

    /// <summary>
    /// Deployment start timestamp (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Deployment completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Deployment failure reason (if failed).
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Number of service instances affected.
    /// </summary>
    public int InstancesAffected { get; set; } = 0;

    /// <summary>
    /// Number of service instances successfully updated.
    /// </summary>
    public int InstancesSucceeded { get; set; } = 0;

    /// <summary>
    /// Number of service instances failed to update.
    /// </summary>
    public int InstancesFailed { get; set; } = 0;

    /// <summary>
    /// Traffic metrics before deployment.
    /// </summary>
    public TrafficMetrics? BaselineMetrics { get; set; }

    /// <summary>
    /// Traffic metrics after deployment.
    /// </summary>
    public TrafficMetrics? CurrentMetrics { get; set; }

    /// <summary>
    /// Deployment configuration (strategy-specific settings).
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();

    /// <summary>
    /// User who initiated the deployment.
    /// </summary>
    public required string InitiatedBy { get; set; }

    /// <summary>
    /// Deployment creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Checks if deployment is in progress.
    /// </summary>
    public bool IsInProgress() => Status == DeploymentStatus.InProgress;

    /// <summary>
    /// Checks if deployment is complete (success or failure).
    /// </summary>
    public bool IsComplete() => Status == DeploymentStatus.Completed ||
                                Status == DeploymentStatus.Failed ||
                                Status == DeploymentStatus.RolledBack;

    /// <summary>
    /// Calculates deployment success rate.
    /// </summary>
    public double GetSuccessRate()
    {
        if (InstancesAffected == 0) return 0;
        return (double)InstancesSucceeded / InstancesAffected * 100;
    }

    /// <summary>
    /// Checks if metrics have degraded compared to baseline.
    /// </summary>
    public bool HasMetricsDegraded()
    {
        if (BaselineMetrics == null || CurrentMetrics == null)
            return false;

        // Check error rate increase
        if (CurrentMetrics.ErrorRate > BaselineMetrics.ErrorRate * 1.5)
            return true;

        // Check latency increase
        if (CurrentMetrics.P95Latency > BaselineMetrics.P95Latency * 1.5)
            return true;

        return false;
    }
}
```

---

## ValidationResult

Represents policy validation result.

**File:** `src/HotSwap.ServiceMesh.Domain/Models/ValidationResult.cs`

```csharp
namespace HotSwap.ServiceMesh.Domain.Models;

/// <summary>
/// Represents a policy validation result.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors.
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings.
    /// </summary>
    public List<ValidationWarning> Warnings { get; set; } = new();

    /// <summary>
    /// Services affected by this policy.
    /// </summary>
    public List<string> AffectedServices { get; set; } = new();

    /// <summary>
    /// Estimated number of instances affected.
    /// </summary>
    public int EstimatedInstancesAffected { get; set; } = 0;

    /// <summary>
    /// Validation timestamp (UTC).
    /// </summary>
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Conflicts with existing policies.
    /// </summary>
    public List<PolicyConflict> Conflicts { get; set; } = new();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success(List<string> affectedServices)
    {
        return new ValidationResult
        {
            IsValid = true,
            AffectedServices = affectedServices
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ValidationResult Failure(List<ValidationError> errors)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = errors
        };
    }
}

/// <summary>
/// Represents a validation error.
/// </summary>
public class ValidationError
{
    public required string Code { get; set; }
    public required string Message { get; set; }
    public string? Path { get; set; }
    public string? Suggestion { get; set; }
}

/// <summary>
/// Represents a validation warning.
/// </summary>
public class ValidationWarning
{
    public required string Code { get; set; }
    public required string Message { get; set; }
    public string? Path { get; set; }
}

/// <summary>
/// Represents a policy conflict.
/// </summary>
public class PolicyConflict
{
    public required string ConflictingPolicyId { get; set; }
    public required string ConflictingPolicyName { get; set; }
    public required string ConflictType { get; set; }
    public required string Description { get; set; }
}
```

---

## ServiceMeshCluster

Represents a service mesh cluster.

**File:** `src/HotSwap.ServiceMesh.Domain/Models/ServiceMeshCluster.cs`

```csharp
namespace HotSwap.ServiceMesh.Domain.Models;

/// <summary>
/// Represents a service mesh cluster.
/// </summary>
public class ServiceMeshCluster
{
    /// <summary>
    /// Unique cluster identifier.
    /// </summary>
    public required string ClusterId { get; set; }

    /// <summary>
    /// Cluster name (e.g., "production-us-east", "staging").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Environment (Dev, Staging, Production).
    /// </summary>
    public required string Environment { get; set; }

    /// <summary>
    /// Service mesh type (Istio or Linkerd).
    /// </summary>
    public required ServiceMeshType MeshType { get; set; }

    /// <summary>
    /// Service mesh version (e.g., "1.20.0").
    /// </summary>
    public required string MeshVersion { get; set; }

    /// <summary>
    /// Kubernetes API server endpoint.
    /// </summary>
    public required string KubernetesEndpoint { get; set; }

    /// <summary>
    /// Number of services in the cluster.
    /// </summary>
    public int ServiceCount { get; set; } = 0;

    /// <summary>
    /// Number of service instances (pods) in the cluster.
    /// </summary>
    public int InstanceCount { get; set; } = 0;

    /// <summary>
    /// Cluster health status.
    /// </summary>
    public ClusterHealth Health { get; set; } = new();

    /// <summary>
    /// Last health check timestamp (UTC).
    /// </summary>
    public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Cluster creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Checks if the cluster is healthy.
    /// </summary>
    public bool IsHealthy() => Health.IsHealthy;
}

/// <summary>
/// Cluster health information.
/// </summary>
public class ClusterHealth
{
    /// <summary>
    /// Overall health status.
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// Kubernetes API reachable.
    /// </summary>
    public bool KubernetesApiReachable { get; set; } = true;

    /// <summary>
    /// Service mesh control plane healthy.
    /// </summary>
    public bool ControlPlaneHealthy { get; set; } = true;

    /// <summary>
    /// Prometheus metrics available.
    /// </summary>
    public bool MetricsAvailable { get; set; } = true;

    /// <summary>
    /// Health check errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
```

---

## Enumerations

### PolicyType

**File:** `src/HotSwap.ServiceMesh.Domain/Enums/PolicyType.cs`

```csharp
namespace HotSwap.ServiceMesh.Domain.Enums;

/// <summary>
/// Represents the type of a service mesh policy.
/// </summary>
public enum PolicyType
{
    // Istio Policy Types
    VirtualService,
    DestinationRule,
    Gateway,
    ServiceEntry,
    AuthorizationPolicy,
    RequestAuthentication,
    PeerAuthentication,
    Sidecar,
    EnvoyFilter,

    // Linkerd Policy Types
    Server,
    ServerAuthorization,
    HTTPRoute,
    TrafficSplit,
    ServiceProfile
}
```

### ServiceMeshType

```csharp
namespace HotSwap.ServiceMesh.Domain.Enums;

/// <summary>
/// Represents the type of service mesh.
/// </summary>
public enum ServiceMeshType
{
    /// <summary>
    /// Istio service mesh.
    /// </summary>
    Istio,

    /// <summary>
    /// Linkerd service mesh.
    /// </summary>
    Linkerd
}
```

### PolicyStatus

```csharp
namespace HotSwap.ServiceMesh.Domain.Enums;

/// <summary>
/// Represents the status of a policy in the approval workflow.
/// </summary>
public enum PolicyStatus
{
    /// <summary>
    /// Policy is in draft state (not yet submitted for approval).
    /// </summary>
    Draft,

    /// <summary>
    /// Policy is pending admin approval.
    /// </summary>
    PendingApproval,

    /// <summary>
    /// Policy is approved for production use.
    /// </summary>
    Approved,

    /// <summary>
    /// Policy has been rejected.
    /// </summary>
    Rejected,

    /// <summary>
    /// Policy is deprecated (marked for removal).
    /// </summary>
    Deprecated
}
```

### DeploymentStrategy

```csharp
namespace HotSwap.ServiceMesh.Domain.Enums;

/// <summary>
/// Represents the deployment strategy for a policy.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Direct deployment (immediate 100% rollout).
    /// </summary>
    Direct,

    /// <summary>
    /// Canary deployment (gradual percentage rollout).
    /// </summary>
    Canary,

    /// <summary>
    /// Blue-green deployment (instant switch).
    /// </summary>
    BlueGreen,

    /// <summary>
    /// Rolling deployment (instance-by-instance).
    /// </summary>
    Rolling,

    /// <summary>
    /// A/B testing deployment (comparative testing).
    /// </summary>
    ABTesting
}
```

### DeploymentStatus

```csharp
namespace HotSwap.ServiceMesh.Domain.Enums;

/// <summary>
/// Represents the status of a deployment.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment is queued, awaiting execution.
    /// </summary>
    Pending,

    /// <summary>
    /// Deployment is currently in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Deployment completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Deployment failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Deployment was rolled back.
    /// </summary>
    RolledBack,

    /// <summary>
    /// Deployment is paused (awaiting manual confirmation).
    /// </summary>
    Paused
}
```

---

## Value Objects

### TrafficMetrics

**File:** `src/HotSwap.ServiceMesh.Domain/ValueObjects/TrafficMetrics.cs`

```csharp
namespace HotSwap.ServiceMesh.Domain.ValueObjects;

/// <summary>
/// Represents traffic metrics for a service.
/// </summary>
public class TrafficMetrics
{
    /// <summary>
    /// Request success rate (0-100%).
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Request error rate (0-100%).
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// P50 latency in milliseconds.
    /// </summary>
    public double P50Latency { get; set; }

    /// <summary>
    /// P95 latency in milliseconds.
    /// </summary>
    public double P95Latency { get; set; }

    /// <summary>
    /// P99 latency in milliseconds.
    /// </summary>
    public double P99Latency { get; set; }

    /// <summary>
    /// Requests per second.
    /// </summary>
    public double RequestsPerSecond { get; set; }

    /// <summary>
    /// Connection failures count.
    /// </summary>
    public int ConnectionFailures { get; set; }

    /// <summary>
    /// Circuit breaker trips count.
    /// </summary>
    public int CircuitBreakerTrips { get; set; }

    /// <summary>
    /// Metrics collection timestamp (UTC).
    /// </summary>
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Compares metrics to detect degradation.
    /// </summary>
    public bool HasDegraded(TrafficMetrics baseline)
    {
        // Error rate increased by more than 50%
        if (ErrorRate > baseline.ErrorRate * 1.5)
            return true;

        // P95 latency increased by more than 50%
        if (P95Latency > baseline.P95Latency * 1.5)
            return true;

        // Connection failures increased significantly
        if (ConnectionFailures > baseline.ConnectionFailures * 2)
            return true;

        return false;
    }
}
```

### DeploymentResult

```csharp
namespace HotSwap.ServiceMesh.Domain.ValueObjects;

/// <summary>
/// Result of a deployment operation.
/// </summary>
public class DeploymentResult
{
    /// <summary>
    /// Whether deployment was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Deployment status message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Number of instances updated.
    /// </summary>
    public int InstancesUpdated { get; set; }

    /// <summary>
    /// Deployment duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Deployment timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static DeploymentResult SuccessResult(int instancesUpdated, TimeSpan duration)
    {
        return new DeploymentResult
        {
            Success = true,
            InstancesUpdated = instancesUpdated,
            Duration = duration,
            Message = $"Successfully deployed to {instancesUpdated} instances in {duration.TotalSeconds:F2}s"
        };
    }

    public static DeploymentResult Failure(string message)
    {
        return new DeploymentResult
        {
            Success = false,
            Message = message
        };
    }
}
```

---

## Validation Examples

### Policy Validation

```csharp
var policy = new Policy
{
    PolicyId = Guid.NewGuid().ToString(),
    Name = "user-service-circuit-breaker",
    Type = PolicyType.DestinationRule,
    ServiceMesh = ServiceMeshType.Istio,
    TargetService = "user-service",
    Namespace = "production",
    OwnerId = "user-123",
    Spec = new PolicySpec
    {
        YamlConfig = @"
apiVersion: networking.istio.io/v1beta1
kind: DestinationRule
metadata:
  name: user-service
spec:
  host: user-service
  trafficPolicy:
    connectionPool:
      tcp:
        maxConnections: 100
      http:
        http1MaxPendingRequests: 50
        http2MaxRequests: 100
    outlierDetection:
      consecutiveErrors: 5
      interval: 30s
      baseEjectionTime: 30s
"
    }
};

if (!policy.IsValid(out var errors))
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation Error: {error}");
    }
}
```

### Deployment Validation

```csharp
var deployment = new Deployment
{
    DeploymentId = Guid.NewGuid().ToString(),
    PolicyId = policy.PolicyId,
    Environment = "Production",
    ClusterId = "prod-us-east-1",
    Strategy = DeploymentStrategy.Canary,
    CanaryPercentage = 10,
    InitiatedBy = "admin@example.com"
};

if (deployment.IsInProgress())
{
    Console.WriteLine("Deployment in progress...");
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.ServiceMesh.Domain`
