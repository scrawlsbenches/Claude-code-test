namespace HotSwap.Distributed.Infrastructure.Data.Entities;

/// <summary>
/// Database entity for deployment jobs using transactional outbox pattern.
/// Replaces fire-and-forget Task.Run with durable job queue.
/// </summary>
public class DeploymentJobEntity
{
    /// <summary>
    /// Auto-increment primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique deployment ID.
    /// </summary>
    public string DeploymentId { get; set; } = string.Empty;

    /// <summary>
    /// Job status.
    /// </summary>
    public JobStatus Status { get; set; }

    /// <summary>
    /// Serialized JSON payload containing deployment request.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// When the job was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When processing started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When processing completed (success or failure).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if job failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Maximum number of retries allowed.
    /// </summary>
    public int MaxRetries { get; set; }

    /// <summary>
    /// When the next retry should be attempted (exponential backoff).
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// Lock expiration time (prevents duplicate processing).
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// Instance ID of the worker processing this job.
    /// </summary>
    public string? ProcessingInstance { get; set; }
}

/// <summary>
/// Job status enum.
/// </summary>
public enum JobStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4
}
