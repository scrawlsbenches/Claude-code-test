namespace HotSwap.Distributed.Api.Models;

#region Request Models

/// <summary>
/// Request to create a new deployment.
/// </summary>
public class CreateDeploymentRequest
{
    public required string ModuleName { get; set; }
    public required string Version { get; set; }
    public required string TargetEnvironment { get; set; }
    public required string RequesterEmail { get; set; }
    public bool RequireApproval { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Request to approve or reject a deployment.
/// </summary>
public class ApprovalDecisionRequest
{
    public required string ApproverEmail { get; set; }
    public required bool Approved { get; set; }
    public string? Reason { get; set; }
}

#endregion

#region Response Models

/// <summary>
/// Response for deployment creation.
/// </summary>
public class DeploymentResponse
{
    public Guid ExecutionId { get; set; }
    public required string Status { get; set; }
    public DateTime StartTime { get; set; }
    public required string EstimatedDuration { get; set; }
    public required string TraceId { get; set; }
    public required Dictionary<string, string> Links { get; set; }
}

/// <summary>
/// Deployment status and results.
/// </summary>
public class DeploymentStatusResponse
{
    public Guid ExecutionId { get; set; }
    public required string ModuleName { get; set; }
    public required string Version { get; set; }
    public required string Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public required string Duration { get; set; }
    public required List<StageResult> Stages { get; set; }
    public string? TraceId { get; set; }
}

/// <summary>
/// Pipeline stage result.
/// </summary>
public class StageResult
{
    public required string Name { get; set; }
    public required string Status { get; set; }
    public DateTime StartTime { get; set; }
    public required string Duration { get; set; }
    public string? Strategy { get; set; }
    public int? NodesDeployed { get; set; }
    public int? NodesFailed { get; set; }
    public required string Message { get; set; }
}

/// <summary>
/// Rollback response.
/// </summary>
public class RollbackResponse
{
    public Guid RollbackId { get; set; }
    public required string Status { get; set; }
    public int NodesAffected { get; set; }
}

/// <summary>
/// Deployment summary for list view.
/// </summary>
public class DeploymentSummary
{
    public Guid ExecutionId { get; set; }
    public required string ModuleName { get; set; }
    public required string Version { get; set; }
    public required string Status { get; set; }
    public DateTime StartTime { get; set; }
    public required string Duration { get; set; }
}

/// <summary>
/// Cluster information response.
/// </summary>
public class ClusterInfoResponse
{
    public required string Environment { get; set; }
    public int TotalNodes { get; set; }
    public int HealthyNodes { get; set; }
    public int UnhealthyNodes { get; set; }
    public required ClusterMetrics Metrics { get; set; }
    public required List<NodeSummary> Nodes { get; set; }
}

/// <summary>
/// Cluster metrics.
/// </summary>
public class ClusterMetrics
{
    public double AvgCpuUsage { get; set; }
    public double AvgMemoryUsage { get; set; }
    public double AvgLatency { get; set; }
    public double ErrorRate { get; set; }
    public double RequestsPerSecond { get; set; }
}

/// <summary>
/// Node summary.
/// </summary>
public class NodeSummary
{
    public Guid NodeId { get; set; }
    public required string Hostname { get; set; }
    public required string Status { get; set; }
    public DateTime LastHeartbeat { get; set; }
}

/// <summary>
/// Cluster metrics time-series response.
/// </summary>
public class ClusterMetricsTimeSeriesResponse
{
    public required string Environment { get; set; }
    public required string Interval { get; set; }
    public required List<MetricsDataPoint> DataPoints { get; set; }
}

/// <summary>
/// Single metrics data point.
/// </summary>
public class MetricsDataPoint
{
    public DateTime Timestamp { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double Latency { get; set; }
    public double ErrorRate { get; set; }
}

/// <summary>
/// Cluster summary.
/// </summary>
public class ClusterSummary
{
    public required string Environment { get; set; }
    public int TotalNodes { get; set; }
    public int HealthyNodes { get; set; }
    public bool IsHealthy { get; set; }
}

/// <summary>
/// Error response.
/// </summary>
public class ErrorResponse
{
    public required string Error { get; set; }
    public string? Details { get; set; }
}

/// <summary>
/// Approval request response.
/// </summary>
public class ApprovalResponse
{
    public Guid ApprovalId { get; set; }
    public Guid DeploymentExecutionId { get; set; }
    public required string ModuleName { get; set; }
    public required string Version { get; set; }
    public required string TargetEnvironment { get; set; }
    public required string RequesterEmail { get; set; }
    public required string Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? RespondedBy { get; set; }
    public string? ResponseReason { get; set; }
    public DateTime TimeoutAt { get; set; }
}

/// <summary>
/// Pending approval summary.
/// </summary>
public class PendingApprovalSummary
{
    public Guid ApprovalId { get; set; }
    public Guid DeploymentExecutionId { get; set; }
    public required string ModuleName { get; set; }
    public required string Version { get; set; }
    public required string TargetEnvironment { get; set; }
    public required string RequesterEmail { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime TimeoutAt { get; set; }
    public required string TimeRemaining { get; set; }
}

#endregion
