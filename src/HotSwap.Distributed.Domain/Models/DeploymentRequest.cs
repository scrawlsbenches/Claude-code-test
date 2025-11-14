using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Request to deploy a module through the pipeline.
/// </summary>
public class DeploymentRequest
{
    /// <summary>
    /// Module to deploy.
    /// </summary>
    public required ModuleDescriptor Module { get; set; }

    /// <summary>
    /// Target environment for deployment.
    /// </summary>
    public EnvironmentType TargetEnvironment { get; set; }

    /// <summary>
    /// Email of the person requesting deployment.
    /// </summary>
    public required string RequesterEmail { get; set; }

    /// <summary>
    /// Whether approval is required for this deployment.
    /// </summary>
    public bool RequireApproval { get; set; } = false;

    /// <summary>
    /// Additional metadata for tracking (JIRA ticket, release notes, etc.).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Unique execution ID for this deployment.
    /// </summary>
    public Guid ExecutionId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Timestamp when the request was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request to deploy a module to specific nodes.
/// </summary>
public class ModuleDeploymentRequest
{
    /// <summary>
    /// Module name.
    /// </summary>
    public required string ModuleName { get; set; }

    /// <summary>
    /// Module version.
    /// </summary>
    public required Version Version { get; set; }

    /// <summary>
    /// Module binary data.
    /// </summary>
    public byte[]? ModuleData { get; set; }

    /// <summary>
    /// Module descriptor with metadata.
    /// </summary>
    public ModuleDescriptor? Descriptor { get; set; }

    /// <summary>
    /// Deployment type (HotSwap, ColdStart, etc.).
    /// </summary>
    public string DeploymentType { get; set; } = "HotSwap";

    /// <summary>
    /// Trace context for distributed tracing.
    /// </summary>
    public Dictionary<string, string> TraceContext { get; set; } = new();
}
