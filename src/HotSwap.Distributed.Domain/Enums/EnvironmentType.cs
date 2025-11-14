namespace HotSwap.Distributed.Domain.Enums;

/// <summary>
/// Represents the deployment environment type.
/// </summary>
public enum EnvironmentType
{
    /// <summary>
    /// Development environment - uses Direct deployment strategy.
    /// </summary>
    Development,

    /// <summary>
    /// QA/Testing environment - uses Rolling deployment strategy.
    /// </summary>
    QA,

    /// <summary>
    /// Staging/Pre-production environment - uses Blue-Green deployment strategy.
    /// </summary>
    Staging,

    /// <summary>
    /// Production environment - uses Canary deployment strategy.
    /// </summary>
    Production
}
