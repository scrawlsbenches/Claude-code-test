namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the operational status of a tenant.
/// </summary>
public enum TenantStatus
{
    /// <summary>
    /// Tenant is being provisioned (creating resources).
    /// </summary>
    Provisioning,

    /// <summary>
    /// Tenant is active and operational.
    /// </summary>
    Active,

    /// <summary>
    /// Tenant is suspended (typically due to payment failure or policy violation).
    /// </summary>
    Suspended,

    /// <summary>
    /// Tenant is being deprovisioned (resources being cleaned up).
    /// </summary>
    Deprovisioning,

    /// <summary>
    /// Tenant has been deleted (tombstone record).
    /// </summary>
    Deleted
}
