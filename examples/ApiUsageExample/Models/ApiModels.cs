using System.Text.Json.Serialization;

namespace HotSwap.Examples.ApiUsage.Models;

#region Request Models

/// <summary>
/// Request to create a new deployment.
/// </summary>
public class CreateDeploymentRequest
{
    [JsonPropertyName("moduleName")]
    public required string ModuleName { get; set; }

    [JsonPropertyName("version")]
    public required string Version { get; set; }

    [JsonPropertyName("targetEnvironment")]
    public required string TargetEnvironment { get; set; }

    [JsonPropertyName("requesterEmail")]
    public required string RequesterEmail { get; set; }

    [JsonPropertyName("requireApproval")]
    public bool RequireApproval { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

#endregion

#region Response Models

/// <summary>
/// Response for deployment creation.
/// </summary>
public class DeploymentResponse
{
    [JsonPropertyName("executionId")]
    public Guid ExecutionId { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    [JsonPropertyName("estimatedDuration")]
    public required string EstimatedDuration { get; set; }

    [JsonPropertyName("traceId")]
    public required string TraceId { get; set; }

    [JsonPropertyName("links")]
    public required Dictionary<string, string> Links { get; set; }
}

/// <summary>
/// Deployment status and results.
/// </summary>
public class DeploymentStatusResponse
{
    [JsonPropertyName("executionId")]
    public Guid ExecutionId { get; set; }

    [JsonPropertyName("moduleName")]
    public required string ModuleName { get; set; }

    [JsonPropertyName("version")]
    public required string Version { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime EndTime { get; set; }

    [JsonPropertyName("duration")]
    public required string Duration { get; set; }

    [JsonPropertyName("stages")]
    public required List<StageResult> Stages { get; set; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }
}

/// <summary>
/// Pipeline stage result.
/// </summary>
public class StageResult
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    [JsonPropertyName("duration")]
    public required string Duration { get; set; }

    [JsonPropertyName("strategy")]
    public string? Strategy { get; set; }

    [JsonPropertyName("nodesDeployed")]
    public int? NodesDeployed { get; set; }

    [JsonPropertyName("nodesFailed")]
    public int? NodesFailed { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }
}

/// <summary>
/// Rollback response.
/// </summary>
public class RollbackResponse
{
    [JsonPropertyName("rollbackId")]
    public Guid RollbackId { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("nodesAffected")]
    public int NodesAffected { get; set; }
}

/// <summary>
/// Deployment summary for list view.
/// </summary>
public class DeploymentSummary
{
    [JsonPropertyName("executionId")]
    public Guid ExecutionId { get; set; }

    [JsonPropertyName("moduleName")]
    public required string ModuleName { get; set; }

    [JsonPropertyName("version")]
    public required string Version { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    [JsonPropertyName("duration")]
    public required string Duration { get; set; }
}

/// <summary>
/// Cluster information response.
/// </summary>
public class ClusterInfoResponse
{
    [JsonPropertyName("environment")]
    public required string Environment { get; set; }

    [JsonPropertyName("totalNodes")]
    public int TotalNodes { get; set; }

    [JsonPropertyName("healthyNodes")]
    public int HealthyNodes { get; set; }

    [JsonPropertyName("unhealthyNodes")]
    public int UnhealthyNodes { get; set; }

    [JsonPropertyName("metrics")]
    public required ClusterMetrics Metrics { get; set; }

    [JsonPropertyName("nodes")]
    public required List<NodeSummary> Nodes { get; set; }
}

/// <summary>
/// Cluster metrics.
/// </summary>
public class ClusterMetrics
{
    [JsonPropertyName("avgCpuUsage")]
    public double AvgCpuUsage { get; set; }

    [JsonPropertyName("avgMemoryUsage")]
    public double AvgMemoryUsage { get; set; }

    [JsonPropertyName("avgLatency")]
    public double AvgLatency { get; set; }

    [JsonPropertyName("errorRate")]
    public double ErrorRate { get; set; }

    [JsonPropertyName("requestsPerSecond")]
    public double RequestsPerSecond { get; set; }
}

/// <summary>
/// Node summary.
/// </summary>
public class NodeSummary
{
    [JsonPropertyName("nodeId")]
    public Guid NodeId { get; set; }

    [JsonPropertyName("hostname")]
    public required string Hostname { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("lastHeartbeat")]
    public DateTime LastHeartbeat { get; set; }
}

/// <summary>
/// Cluster metrics time-series response.
/// </summary>
public class ClusterMetricsTimeSeriesResponse
{
    [JsonPropertyName("environment")]
    public required string Environment { get; set; }

    [JsonPropertyName("interval")]
    public required string Interval { get; set; }

    [JsonPropertyName("dataPoints")]
    public required List<MetricsDataPoint> DataPoints { get; set; }
}

/// <summary>
/// Single metrics data point.
/// </summary>
public class MetricsDataPoint
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("cpuUsage")]
    public double CpuUsage { get; set; }

    [JsonPropertyName("memoryUsage")]
    public double MemoryUsage { get; set; }

    [JsonPropertyName("latency")]
    public double Latency { get; set; }

    [JsonPropertyName("errorRate")]
    public double ErrorRate { get; set; }
}

/// <summary>
/// Cluster summary.
/// </summary>
public class ClusterSummary
{
    [JsonPropertyName("environment")]
    public required string Environment { get; set; }

    [JsonPropertyName("totalNodes")]
    public int TotalNodes { get; set; }

    [JsonPropertyName("healthyNodes")]
    public int HealthyNodes { get; set; }

    [JsonPropertyName("isHealthy")]
    public bool IsHealthy { get; set; }
}

/// <summary>
/// Error response.
/// </summary>
public class ErrorResponse
{
    [JsonPropertyName("error")]
    public required string Error { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }
}

#endregion
