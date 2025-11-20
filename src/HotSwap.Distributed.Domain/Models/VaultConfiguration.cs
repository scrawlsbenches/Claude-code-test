namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Configuration for HashiCorp Vault connection and authentication.
/// </summary>
public class VaultConfiguration
{
    /// <summary>
    /// Vault server URL (e.g., "https://vault.example.com:8200").
    /// </summary>
    public string VaultUrl { get; set; } = string.Empty;

    /// <summary>
    /// Vault namespace (for Vault Enterprise). Leave empty for Vault OSS.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Secrets engine mount point (default: "secret" for KV v2).
    /// </summary>
    public string MountPoint { get; set; } = "secret";

    /// <summary>
    /// Authentication method: Token, AppRole, Kubernetes, UserPass.
    /// </summary>
    public VaultAuthMethod AuthMethod { get; set; } = VaultAuthMethod.Token;

    /// <summary>
    /// Vault token for Token authentication.
    /// Should be loaded from environment variable in production.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// AppRole role ID for AppRole authentication.
    /// </summary>
    public string? AppRoleId { get; set; }

    /// <summary>
    /// AppRole secret ID for AppRole authentication.
    /// Should be loaded from environment variable in production.
    /// </summary>
    public string? AppRoleSecretId { get; set; }

    /// <summary>
    /// Connection timeout in seconds (default: 30).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of retry attempts for transient failures (default: 3).
    /// </summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Retry delay in milliseconds (default: 1000ms).
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Enable TLS certificate validation (should be true in production).
    /// </summary>
    public bool ValidateCertificate { get; set; } = true;
}

/// <summary>
/// Supported Vault authentication methods.
/// </summary>
public enum VaultAuthMethod
{
    /// <summary>
    /// Token-based authentication (simplest, for development and service accounts).
    /// </summary>
    Token,

    /// <summary>
    /// AppRole authentication (recommended for applications and automation).
    /// </summary>
    AppRole,

    /// <summary>
    /// Kubernetes authentication (for K8s pods).
    /// </summary>
    Kubernetes,

    /// <summary>
    /// Username/Password authentication (for human users).
    /// </summary>
    UserPass
}
