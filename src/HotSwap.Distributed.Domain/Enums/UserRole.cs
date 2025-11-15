namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Defines user roles for role-based access control (RBAC).
/// </summary>
public enum UserRole
{
    /// <summary>
    /// View-only access to deployments and metrics.
    /// Can read deployment status, metrics, and cluster information.
    /// </summary>
    Viewer = 1,

    /// <summary>
    /// Can create and manage deployments.
    /// Includes all Viewer permissions plus deployment creation and rollback.
    /// </summary>
    Deployer = 2,

    /// <summary>
    /// Full administrative access.
    /// Includes all Deployer permissions plus approval workflow management,
    /// user management, and system configuration.
    /// </summary>
    Admin = 3
}
