namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Deployment strategy for module updates.
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Deploy to all nodes simultaneously.
    /// Fastest but highest risk.
    /// </summary>
    Direct,

    /// <summary>
    /// Deploy sequentially with health checks.
    /// Gradual rollout with automatic rollback on failure.
    /// </summary>
    Rolling,

    /// <summary>
    /// Deploy to parallel environment and switch traffic.
    /// Zero-downtime with instant rollback capability.
    /// </summary>
    BlueGreen,

    /// <summary>
    /// Gradual rollout with metrics analysis.
    /// Safest for production deployments.
    /// </summary>
    Canary
}
