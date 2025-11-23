namespace HotSwap.Distributed.Infrastructure.Storage;

/// <summary>
/// Configuration for MinIO object storage connection.
/// </summary>
public class MinioConfiguration
{
    /// <summary>
    /// MinIO server endpoint (e.g., "localhost:9000" or "minio.example.com:9000").
    /// </summary>
    public string Endpoint { get; set; } = "localhost:9000";

    /// <summary>
    /// Access key for authentication.
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Secret key for authentication.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Use SSL/TLS for connections (HTTPS).
    /// </summary>
    public bool UseSSL { get; set; } = false;

    /// <summary>
    /// AWS region (optional, for S3 compatibility).
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Connection timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to validate the configuration on startup.
    /// </summary>
    public bool ValidateOnStartup { get; set; } = true;
}
