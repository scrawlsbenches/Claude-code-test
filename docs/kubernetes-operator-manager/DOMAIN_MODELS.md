# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.Kubernetes.Domain.Models`

---

## Table of Contents

1. [Operator](#operator)
2. [OperatorDeployment](#operatordeployment)
3. [KubernetesCluster](#kubernetescluster)
4. [CustomResourceDefinition](#customresourcedefinition)
5. [OperatorHealth](#operatorhealth)
6. [Enumerations](#enumerations)
7. [Value Objects](#value-objects)

---

## Operator

Represents a Kubernetes operator with versioning and chart information.

**File:** `src/HotSwap.Kubernetes.Domain/Models/Operator.cs`

```csharp
namespace HotSwap.Kubernetes.Domain.Models;

/// <summary>
/// Represents a Kubernetes operator managed by the system.
/// </summary>
public class Operator
{
    /// <summary>
    /// Unique operator identifier (GUID format).
    /// </summary>
    public required string OperatorId { get; set; }

    /// <summary>
    /// Operator name (e.g., "cert-manager", "istio-operator").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Kubernetes namespace where operator is installed.
    /// </summary>
    public required string Namespace { get; set; }

    /// <summary>
    /// Helm chart repository URL.
    /// </summary>
    public required string ChartRepository { get; set; }

    /// <summary>
    /// Helm chart name within the repository.
    /// </summary>
    public required string ChartName { get; set; }

    /// <summary>
    /// Currently deployed operator version (semantic versioning).
    /// </summary>
    public string? CurrentVersion { get; set; }

    /// <summary>
    /// Latest available version in chart repository.
    /// </summary>
    public string? LatestVersion { get; set; }

    /// <summary>
    /// CRD names associated with this operator.
    /// </summary>
    public List<string> CRDNames { get; set; } = new();

    /// <summary>
    /// Operator labels for categorization and filtering.
    /// </summary>
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>
    /// Default Helm values for deployment.
    /// </summary>
    public Dictionary<string, object> DefaultValues { get; set; } = new();

    /// <summary>
    /// Operator creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of clusters where this operator is deployed.
    /// </summary>
    public int DeployedClusterCount { get; set; } = 0;

    /// <summary>
    /// Validates the operator configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(OperatorId))
            errors.Add("OperatorId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Name, @"^[a-z0-9]([-a-z0-9]*[a-z0-9])?$"))
            errors.Add("Name must be a valid Kubernetes name (lowercase alphanumeric and hyphens)");

        if (string.IsNullOrWhiteSpace(Namespace))
            errors.Add("Namespace is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Namespace, @"^[a-z0-9]([-a-z0-9]*[a-z0-9])?$"))
            errors.Add("Namespace must be a valid Kubernetes namespace");

        if (string.IsNullOrWhiteSpace(ChartRepository))
            errors.Add("ChartRepository is required");
        else if (!Uri.IsWellFormedUriString(ChartRepository, UriKind.Absolute))
            errors.Add("ChartRepository must be a valid URL");

        if (string.IsNullOrWhiteSpace(ChartName))
            errors.Add("ChartName is required");

        if (CurrentVersion != null && !IsValidSemanticVersion(CurrentVersion))
            errors.Add("CurrentVersion must be valid semantic version (e.g., v1.2.3)");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if version is valid semantic versioning format.
    /// </summary>
    private bool IsValidSemanticVersion(string version)
    {
        var pattern = @"^v?\d+\.\d+\.\d+(-[\w\.]+)?$";
        return System.Text.RegularExpressions.Regex.IsMatch(version, pattern);
    }

    /// <summary>
    /// Checks if operator has a newer version available.
    /// </summary>
    public bool HasUpdate() =>
        CurrentVersion != null &&
        LatestVersion != null &&
        CurrentVersion != LatestVersion;
}
```

---

## OperatorDeployment

Represents a deployment execution for an operator across clusters.

**File:** `src/HotSwap.Kubernetes.Domain/Models/OperatorDeployment.cs`

```csharp
namespace HotSwap.Kubernetes.Domain.Models;

/// <summary>
/// Represents an operator deployment execution.
/// </summary>
public class OperatorDeployment
{
    /// <summary>
    /// Unique deployment identifier (GUID format).
    /// </summary>
    public required string DeploymentId { get; set; }

    /// <summary>
    /// Reference to the operator being deployed.
    /// </summary>
    public required string OperatorId { get; set; }

    /// <summary>
    /// Operator name (denormalized for convenience).
    /// </summary>
    public required string OperatorName { get; set; }

    /// <summary>
    /// Target operator version to deploy.
    /// </summary>
    public required string TargetVersion { get; set; }

    /// <summary>
    /// Previous operator version (for rollback).
    /// </summary>
    public string? PreviousVersion { get; set; }

    /// <summary>
    /// Deployment strategy to use.
    /// </summary>
    public DeploymentStrategy Strategy { get; set; } = DeploymentStrategy.Rolling;

    /// <summary>
    /// Target cluster names for deployment.
    /// </summary>
    public List<string> TargetClusters { get; set; } = new();

    /// <summary>
    /// Current deployment status.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Planning;

    /// <summary>
    /// Strategy-specific configuration (JSON serialized).
    /// </summary>
    public string? StrategyConfig { get; set; }

    /// <summary>
    /// Helm values override for this deployment.
    /// </summary>
    public Dictionary<string, object> HelmValues { get; set; } = new();

    /// <summary>
    /// Deployment start timestamp (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Deployment completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// User who initiated the deployment.
    /// </summary>
    public required string InitiatedBy { get; set; }

    /// <summary>
    /// Approval status for production deployments.
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;

    /// <summary>
    /// Approver user (if approved).
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Per-cluster deployment status tracking.
    /// </summary>
    public List<ClusterDeploymentStatus> ClusterStatuses { get; set; } = new();

    /// <summary>
    /// Error message (if deployment failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether automatic rollback is enabled.
    /// </summary>
    public bool AutoRollbackEnabled { get; set; } = true;

    /// <summary>
    /// Rollback reason (if rolled back).
    /// </summary>
    public string? RollbackReason { get; set; }

    /// <summary>
    /// Rollback timestamp (UTC).
    /// </summary>
    public DateTime? RolledBackAt { get; set; }

    /// <summary>
    /// Validates the deployment configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DeploymentId))
            errors.Add("DeploymentId is required");

        if (string.IsNullOrWhiteSpace(OperatorId))
            errors.Add("OperatorId is required");

        if (string.IsNullOrWhiteSpace(TargetVersion))
            errors.Add("TargetVersion is required");

        if (TargetClusters.Count == 0)
            errors.Add("At least one target cluster is required");

        if (string.IsNullOrWhiteSpace(InitiatedBy))
            errors.Add("InitiatedBy is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if deployment is in a terminal state.
    /// </summary>
    public bool IsTerminal() =>
        Status == DeploymentStatus.Completed ||
        Status == DeploymentStatus.Failed ||
        Status == DeploymentStatus.RolledBack;

    /// <summary>
    /// Calculates deployment duration.
    /// </summary>
    public TimeSpan? GetDuration()
    {
        if (StartedAt == null) return null;
        var endTime = CompletedAt ?? DateTime.UtcNow;
        return endTime - StartedAt.Value;
    }

    /// <summary>
    /// Gets overall deployment success rate.
    /// </summary>
    public double GetSuccessRate()
    {
        if (ClusterStatuses.Count == 0) return 0;
        var successCount = ClusterStatuses.Count(c =>
            c.Status == DeploymentStatus.Completed);
        return (double)successCount / ClusterStatuses.Count;
    }
}

/// <summary>
/// Tracks deployment status for a specific cluster.
/// </summary>
public class ClusterDeploymentStatus
{
    /// <summary>
    /// Cluster name.
    /// </summary>
    public required string ClusterName { get; set; }

    /// <summary>
    /// Deployment status for this cluster.
    /// </summary>
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Planning;

    /// <summary>
    /// Deployment start time for this cluster (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Deployment completion time for this cluster (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Health check results after deployment.
    /// </summary>
    public OperatorHealth? HealthCheck { get; set; }
}
```

---

## KubernetesCluster

Represents a registered Kubernetes cluster.

**File:** `src/HotSwap.Kubernetes.Domain/Models/KubernetesCluster.cs`

```csharp
namespace HotSwap.Kubernetes.Domain.Models;

/// <summary>
/// Represents a registered Kubernetes cluster.
/// </summary>
public class KubernetesCluster
{
    /// <summary>
    /// Unique cluster identifier.
    /// </summary>
    public required string ClusterId { get; set; }

    /// <summary>
    /// Cluster name (unique).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Environment classification.
    /// </summary>
    public ClusterEnvironment Environment { get; set; } = ClusterEnvironment.Development;

    /// <summary>
    /// Encrypted kubeconfig content (base64).
    /// </summary>
    public required string KubeconfigEncrypted { get; set; }

    /// <summary>
    /// Kubernetes API server URL.
    /// </summary>
    public required string ApiServerUrl { get; set; }

    /// <summary>
    /// Detected Kubernetes version.
    /// </summary>
    public string? KubernetesVersion { get; set; }

    /// <summary>
    /// Number of nodes in the cluster.
    /// </summary>
    public int NodeCount { get; set; } = 0;

    /// <summary>
    /// Current cluster health status.
    /// </summary>
    public ClusterHealthStatus HealthStatus { get; set; } = ClusterHealthStatus.Unknown;

    /// <summary>
    /// Last successful health check timestamp (UTC).
    /// </summary>
    public DateTime? LastHealthCheckAt { get; set; }

    /// <summary>
    /// Cluster labels for categorization.
    /// </summary>
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>
    /// Operators deployed to this cluster.
    /// </summary>
    public List<DeployedOperator> DeployedOperators { get; set; } = new();

    /// <summary>
    /// Cluster registration timestamp (UTC).
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the cluster is enabled for deployments.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Validates the cluster configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ClusterId))
            errors.Add("ClusterId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Name, @"^[a-z0-9]([-a-z0-9]*[a-z0-9])?$"))
            errors.Add("Name must be a valid cluster name (lowercase alphanumeric and hyphens)");

        if (string.IsNullOrWhiteSpace(KubeconfigEncrypted))
            errors.Add("Kubeconfig is required");

        if (string.IsNullOrWhiteSpace(ApiServerUrl))
            errors.Add("ApiServerUrl is required");
        else if (!Uri.IsWellFormedUriString(ApiServerUrl, UriKind.Absolute))
            errors.Add("ApiServerUrl must be a valid URL");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if cluster is healthy based on last health check.
    /// </summary>
    public bool IsHealthy()
    {
        if (!IsEnabled) return false;
        if (HealthStatus != ClusterHealthStatus.Healthy) return false;
        if (LastHealthCheckAt == null) return false;

        // Consider unhealthy if no health check in last 5 minutes
        return DateTime.UtcNow - LastHealthCheckAt.Value < TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Gets operator deployment by operator name.
    /// </summary>
    public DeployedOperator? GetOperatorDeployment(string operatorName)
    {
        return DeployedOperators.FirstOrDefault(o => o.OperatorName == operatorName);
    }
}

/// <summary>
/// Represents an operator deployed to a cluster.
/// </summary>
public class DeployedOperator
{
    /// <summary>
    /// Operator name.
    /// </summary>
    public required string OperatorName { get; set; }

    /// <summary>
    /// Deployed version.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Namespace where operator is deployed.
    /// </summary>
    public required string Namespace { get; set; }

    /// <summary>
    /// Deployment timestamp (UTC).
    /// </summary>
    public DateTime DeployedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last health check result.
    /// </summary>
    public OperatorHealth? HealthStatus { get; set; }
}
```

---

## CustomResourceDefinition

Represents a CRD schema with versioning and compatibility tracking.

**File:** `src/HotSwap.Kubernetes.Domain/Models/CustomResourceDefinition.cs`

```csharp
namespace HotSwap.Kubernetes.Domain.Models;

/// <summary>
/// Represents a Custom Resource Definition.
/// </summary>
public class CustomResourceDefinition
{
    /// <summary>
    /// Unique CRD identifier.
    /// </summary>
    public required string CRDId { get; set; }

    /// <summary>
    /// CRD name (e.g., "certificates.cert-manager.io").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Group name (e.g., "cert-manager.io").
    /// </summary>
    public required string Group { get; set; }

    /// <summary>
    /// API versions supported (e.g., ["v1", "v1alpha1"]).
    /// </summary>
    public List<string> Versions { get; set; } = new();

    /// <summary>
    /// CRD scope (Namespaced or Cluster).
    /// </summary>
    public CRDScope Scope { get; set; } = CRDScope.Namespaced;

    /// <summary>
    /// Associated operator name.
    /// </summary>
    public required string OperatorName { get; set; }

    /// <summary>
    /// Operator version that introduced this CRD.
    /// </summary>
    public required string OperatorVersion { get; set; }

    /// <summary>
    /// CRD schema definition (JSON format).
    /// </summary>
    public required string SchemaDefinition { get; set; }

    /// <summary>
    /// Schema version for tracking changes.
    /// </summary>
    public string SchemaVersion { get; set; } = "1.0";

    /// <summary>
    /// Current CRD status.
    /// </summary>
    public CRDStatus Status { get; set; } = CRDStatus.Active;

    /// <summary>
    /// Approval status for production deployment.
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.NotRequired;

    /// <summary>
    /// Admin who approved the CRD (if approved).
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval timestamp (UTC).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// CRD creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// CRD deprecation timestamp (UTC).
    /// </summary>
    public DateTime? DeprecatedAt { get; set; }

    /// <summary>
    /// Deprecation reason (if deprecated).
    /// </summary>
    public string? DeprecationReason { get; set; }

    /// <summary>
    /// Migration guide URL (if deprecated).
    /// </summary>
    public string? MigrationGuide { get; set; }

    /// <summary>
    /// Schema change history.
    /// </summary>
    public List<SchemaChange> SchemaHistory { get; set; } = new();

    /// <summary>
    /// Validates the CRD configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(CRDId))
            errors.Add("CRDId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(Group))
            errors.Add("Group is required");

        if (Versions.Count == 0)
            errors.Add("At least one version is required");

        if (string.IsNullOrWhiteSpace(OperatorName))
            errors.Add("OperatorName is required");

        if (string.IsNullOrWhiteSpace(SchemaDefinition))
            errors.Add("SchemaDefinition is required");

        // Validate JSON Schema format
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(SchemaDefinition);
        }
        catch
        {
            errors.Add("SchemaDefinition must be valid JSON");
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if CRD is approved for production use.
    /// </summary>
    public bool IsApproved() => ApprovalStatus == ApprovalStatus.Approved;

    /// <summary>
    /// Checks if CRD is deprecated.
    /// </summary>
    public bool IsDeprecated() => Status == CRDStatus.Deprecated;
}

/// <summary>
/// Represents a schema change in CRD history.
/// </summary>
public class SchemaChange
{
    /// <summary>
    /// Change type (Added, Modified, Removed).
    /// </summary>
    public SchemaChangeType ChangeType { get; set; }

    /// <summary>
    /// Field path that changed (e.g., "spec.secretName").
    /// </summary>
    public required string FieldPath { get; set; }

    /// <summary>
    /// Previous field type (if modified).
    /// </summary>
    public string? PreviousType { get; set; }

    /// <summary>
    /// New field type.
    /// </summary>
    public string? NewType { get; set; }

    /// <summary>
    /// Whether this is a breaking change.
    /// </summary>
    public bool IsBreaking { get; set; } = false;

    /// <summary>
    /// Change description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Operator version that introduced this change.
    /// </summary>
    public required string OperatorVersion { get; set; }

    /// <summary>
    /// Change timestamp (UTC).
    /// </summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
```

---

## OperatorHealth

Represents operator health status across components.

**File:** `src/HotSwap.Kubernetes.Domain/Models/OperatorHealth.cs`

```csharp
namespace HotSwap.Kubernetes.Domain.Models;

/// <summary>
/// Represents operator health status.
/// </summary>
public class OperatorHealth
{
    /// <summary>
    /// Operator name.
    /// </summary>
    public required string OperatorName { get; set; }

    /// <summary>
    /// Cluster where operator is deployed.
    /// </summary>
    public required string ClusterName { get; set; }

    /// <summary>
    /// Overall health status.
    /// </summary>
    public HealthStatus OverallHealth { get; set; } = HealthStatus.Unknown;

    /// <summary>
    /// Controller pod health.
    /// </summary>
    public PodHealth ControllerPodHealth { get; set; } = new();

    /// <summary>
    /// Webhook health (if operator has webhooks).
    /// </summary>
    public WebhookHealth? WebhookHealth { get; set; }

    /// <summary>
    /// CRD reconciliation health.
    /// </summary>
    public ReconciliationHealth CRDReconciliationHealth { get; set; } = new();

    /// <summary>
    /// Last health check timestamp (UTC).
    /// </summary>
    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Health check error message (if unhealthy).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Consecutive failed health checks.
    /// </summary>
    public int ConsecutiveFailures { get; set; } = 0;

    /// <summary>
    /// Determines overall health based on component health.
    /// </summary>
    public void EvaluateOverallHealth()
    {
        if (!ControllerPodHealth.IsHealthy)
        {
            OverallHealth = HealthStatus.Unhealthy;
            return;
        }

        if (WebhookHealth != null && !WebhookHealth.IsHealthy)
        {
            OverallHealth = HealthStatus.Unhealthy;
            return;
        }

        if (!CRDReconciliationHealth.IsHealthy)
        {
            OverallHealth = HealthStatus.Degraded;
            return;
        }

        OverallHealth = HealthStatus.Healthy;
    }

    /// <summary>
    /// Checks if rollback should be triggered.
    /// </summary>
    public bool ShouldTriggerRollback()
    {
        // Trigger rollback after 3 consecutive failures
        return ConsecutiveFailures >= 3;
    }
}

/// <summary>
/// Controller pod health information.
/// </summary>
public class PodHealth
{
    /// <summary>
    /// Number of ready pods.
    /// </summary>
    public int ReadyPods { get; set; } = 0;

    /// <summary>
    /// Expected number of pods (replicas).
    /// </summary>
    public int ExpectedPods { get; set; } = 1;

    /// <summary>
    /// Pods in crash loop.
    /// </summary>
    public int CrashLoopPods { get; set; } = 0;

    /// <summary>
    /// Average pod restart count.
    /// </summary>
    public double AverageRestarts { get; set; } = 0;

    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsagePercent { get; set; } = 0;

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    public double MemoryUsagePercent { get; set; } = 0;

    /// <summary>
    /// Whether pod health is healthy.
    /// </summary>
    public bool IsHealthy =>
        ReadyPods >= ExpectedPods &&
        CrashLoopPods == 0 &&
        AverageRestarts < 5;
}

/// <summary>
/// Webhook health information.
/// </summary>
public class WebhookHealth
{
    /// <summary>
    /// Webhook endpoint URL.
    /// </summary>
    public required string EndpointUrl { get; set; }

    /// <summary>
    /// Whether webhook endpoint is reachable.
    /// </summary>
    public bool IsReachable { get; set; } = false;

    /// <summary>
    /// Webhook response latency in milliseconds.
    /// </summary>
    public double LatencyMs { get; set; } = 0;

    /// <summary>
    /// TLS certificate expiration date.
    /// </summary>
    public DateTime? CertificateExpiresAt { get; set; }

    /// <summary>
    /// Last webhook call timestamp (UTC).
    /// </summary>
    public DateTime? LastCallAt { get; set; }

    /// <summary>
    /// Whether webhook health is healthy.
    /// </summary>
    public bool IsHealthy =>
        IsReachable &&
        LatencyMs < 1000 &&
        (CertificateExpiresAt == null || CertificateExpiresAt > DateTime.UtcNow.AddDays(7));
}

/// <summary>
/// CRD reconciliation health information.
/// </summary>
public class ReconciliationHealth
{
    /// <summary>
    /// Number of CRD instances being reconciled.
    /// </summary>
    public int ActiveReconciliations { get; set; } = 0;

    /// <summary>
    /// Number of stale CRD instances (not reconciled recently).
    /// </summary>
    public int StaleReconciliations { get; set; } = 0;

    /// <summary>
    /// Reconciliation error rate (0-1).
    /// </summary>
    public double ErrorRate { get; set; } = 0;

    /// <summary>
    /// Average reconciliation duration in milliseconds.
    /// </summary>
    public double AverageReconciliationMs { get; set; } = 0;

    /// <summary>
    /// Last reconciliation timestamp (UTC).
    /// </summary>
    public DateTime? LastReconciliationAt { get; set; }

    /// <summary>
    /// Whether reconciliation health is healthy.
    /// </summary>
    public bool IsHealthy =>
        StaleReconciliations == 0 &&
        ErrorRate < 0.05 &&
        (LastReconciliationAt == null || DateTime.UtcNow - LastReconciliationAt < TimeSpan.FromMinutes(5));
}
```

---

## Enumerations

### DeploymentStrategy

**File:** `src/HotSwap.Kubernetes.Domain/Enums/DeploymentStrategy.cs`

```csharp
namespace HotSwap.Kubernetes.Domain.Enums;

/// <summary>
/// Represents operator deployment strategy.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Direct deployment to all clusters simultaneously.
    /// </summary>
    Direct,

    /// <summary>
    /// Gradual canary deployment with percentage-based rollout.
    /// </summary>
    Canary,

    /// <summary>
    /// Blue-Green deployment with environment switching.
    /// </summary>
    BlueGreen,

    /// <summary>
    /// Rolling deployment cluster-by-cluster.
    /// </summary>
    Rolling
}
```

### DeploymentStatus

```csharp
namespace HotSwap.Kubernetes.Domain.Enums;

/// <summary>
/// Represents deployment execution status.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment is being planned and validated.
    /// </summary>
    Planning,

    /// <summary>
    /// Deployment is in progress.
    /// </summary>
    Deploying,

    /// <summary>
    /// Deployment is validating health checks.
    /// </summary>
    Validating,

    /// <summary>
    /// Deployment completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Deployment is rolling back due to failures.
    /// </summary>
    RollingBack,

    /// <summary>
    /// Deployment was rolled back.
    /// </summary>
    RolledBack,

    /// <summary>
    /// Deployment failed and requires manual intervention.
    /// </summary>
    Failed
}
```

### ClusterEnvironment

```csharp
namespace HotSwap.Kubernetes.Domain.Enums;

/// <summary>
/// Represents cluster environment classification.
/// </summary>
public enum ClusterEnvironment
{
    /// <summary>
    /// Development environment.
    /// </summary>
    Development,

    /// <summary>
    /// Staging/QA environment.
    /// </summary>
    Staging,

    /// <summary>
    /// Production environment.
    /// </summary>
    Production
}
```

### ClusterHealthStatus

```csharp
namespace HotSwap.Kubernetes.Domain.Enums;

/// <summary>
/// Represents cluster health status.
/// </summary>
public enum ClusterHealthStatus
{
    /// <summary>
    /// Health status unknown (not checked yet).
    /// </summary>
    Unknown,

    /// <summary>
    /// Cluster is healthy and reachable.
    /// </summary>
    Healthy,

    /// <summary>
    /// Cluster is degraded but operational.
    /// </summary>
    Degraded,

    /// <summary>
    /// Cluster is unhealthy or unreachable.
    /// </summary>
    Unhealthy
}
```

### HealthStatus

```csharp
namespace HotSwap.Kubernetes.Domain.Enums;

/// <summary>
/// Represents operator health status.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Health status unknown.
    /// </summary>
    Unknown,

    /// <summary>
    /// Operator is healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// Operator is degraded but functional.
    /// </summary>
    Degraded,

    /// <summary>
    /// Operator is unhealthy.
    /// </summary>
    Unhealthy
}
```

### ApprovalStatus

```csharp
namespace HotSwap.Kubernetes.Domain.Enums;

/// <summary>
/// Represents approval workflow status.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// Approval not required.
    /// </summary>
    NotRequired,

    /// <summary>
    /// Awaiting approval.
    /// </summary>
    Pending,

    /// <summary>
    /// Approved for deployment.
    /// </summary>
    Approved,

    /// <summary>
    /// Approval rejected.
    /// </summary>
    Rejected
}
```

### CRDStatus

```csharp
namespace HotSwap.Kubernetes.Domain.Enums;

/// <summary>
/// Represents CRD lifecycle status.
/// </summary>
public enum CRDStatus
{
    /// <summary>
    /// CRD is active and in use.
    /// </summary>
    Active,

    /// <summary>
    /// CRD is deprecated but still supported.
    /// </summary>
    Deprecated,

    /// <summary>
    /// CRD has been removed.
    /// </summary>
    Removed
}
```

### CRDScope

```csharp
namespace HotSwap.Kubernetes.Domain.Enums;

/// <summary>
/// Represents CRD scope.
/// </summary>
public enum CRDScope
{
    /// <summary>
    /// CRD is namespaced.
    /// </summary>
    Namespaced,

    /// <summary>
    /// CRD is cluster-scoped.
    /// </summary>
    Cluster
}
```

### SchemaChangeType

```csharp
namespace HotSwap.Kubernetes.Domain.Enums;

/// <summary>
/// Represents schema change type.
/// </summary>
public enum SchemaChangeType
{
    /// <summary>
    /// Field was added.
    /// </summary>
    Added,

    /// <summary>
    /// Field was modified (type or validation changed).
    /// </summary>
    Modified,

    /// <summary>
    /// Field was removed.
    /// </summary>
    Removed
}
```

---

## Value Objects

### DeploymentResult

**File:** `src/HotSwap.Kubernetes.Domain/ValueObjects/DeploymentResult.cs`

```csharp
namespace HotSwap.Kubernetes.Domain.ValueObjects;

/// <summary>
/// Result of a deployment operation.
/// </summary>
public class DeploymentResult
{
    /// <summary>
    /// Whether deployment was successful.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// Clusters where deployment succeeded.
    /// </summary>
    public List<string> SuccessfulClusters { get; private set; } = new();

    /// <summary>
    /// Clusters where deployment failed.
    /// </summary>
    public List<string> FailedClusters { get; private set; } = new();

    /// <summary>
    /// Error message (if deployment failed).
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Deployment timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Health check results per cluster.
    /// </summary>
    public Dictionary<string, OperatorHealth> HealthResults { get; private set; } = new();

    public static DeploymentResult SuccessResult(List<string> clusters)
    {
        return new DeploymentResult
        {
            Success = true,
            SuccessfulClusters = clusters
        };
    }

    public static DeploymentResult PartialSuccess(List<string> successful, List<string> failed)
    {
        return new DeploymentResult
        {
            Success = false,
            SuccessfulClusters = successful,
            FailedClusters = failed,
            ErrorMessage = $"Deployment failed on {failed.Count} cluster(s)"
        };
    }

    public static DeploymentResult Failure(string errorMessage)
    {
        return new DeploymentResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
```

---

## Validation Examples

### Operator Validation

```csharp
var @operator = new Operator
{
    OperatorId = Guid.NewGuid().ToString(),
    Name = "cert-manager",
    Namespace = "cert-manager",
    ChartRepository = "https://charts.jetstack.io",
    ChartName = "cert-manager",
    CurrentVersion = "v1.13.0"
};

if (!@operator.IsValid(out var errors))
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation Error: {error}");
    }
}
```

### OperatorDeployment Validation

```csharp
var deployment = new OperatorDeployment
{
    DeploymentId = Guid.NewGuid().ToString(),
    OperatorId = "op-123",
    OperatorName = "cert-manager",
    TargetVersion = "v1.14.0",
    Strategy = DeploymentStrategy.Canary,
    TargetClusters = new List<string> { "prod-us-east", "prod-eu-west" },
    InitiatedBy = "admin@example.com"
};

if (!deployment.IsValid(out var errors))
{
    Console.WriteLine("Deployment validation failed:");
    errors.ForEach(Console.WriteLine);
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.Kubernetes.Domain`
