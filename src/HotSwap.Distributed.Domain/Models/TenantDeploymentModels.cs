using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a tenant-scoped deployment request.
/// </summary>
public class TenantDeploymentRequest : DeploymentRequest
{
    /// <summary>
    /// Tenant ID for this deployment.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Website ID (if deploying to a specific website).
    /// </summary>
    public Guid? WebsiteId { get; set; }

    /// <summary>
    /// Type of module being deployed.
    /// </summary>
    public WebsiteModuleType ModuleType { get; set; }

    /// <summary>
    /// Deployment scope (single website, all tenant websites, etc.).
    /// </summary>
    public DeploymentScope Scope { get; set; }
}

/// <summary>
/// Type of website module being deployed.
/// </summary>
public enum WebsiteModuleType
{
    /// <summary>
    /// Theme deployment.
    /// </summary>
    Theme,

    /// <summary>
    /// Plugin deployment.
    /// </summary>
    Plugin,

    /// <summary>
    /// Content deployment.
    /// </summary>
    Content,

    /// <summary>
    /// Configuration deployment.
    /// </summary>
    Configuration,

    /// <summary>
    /// API module deployment.
    /// </summary>
    Api
}

/// <summary>
/// Scope of deployment (which websites are affected).
/// </summary>
public enum DeploymentScope
{
    /// <summary>
    /// Deploy to a single website.
    /// </summary>
    SingleWebsite,

    /// <summary>
    /// Deploy to all websites owned by a tenant.
    /// </summary>
    AllTenantWebsites,

    /// <summary>
    /// Deploy to specific list of websites.
    /// </summary>
    SpecificWebsites
}

/// <summary>
/// Result of a tenant deployment.
/// </summary>
public class TenantDeploymentResult
{
    /// <summary>
    /// Deployment ID.
    /// </summary>
    public Guid DeploymentId { get; set; }

    /// <summary>
    /// Tenant ID.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Success status.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Result message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Websites affected by this deployment.
    /// </summary>
    public List<Guid> AffectedWebsites { get; set; } = new();

    /// <summary>
    /// Deployment start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Deployment end time.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Any errors that occurred.
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
