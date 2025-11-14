namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Describes a kernel module with its metadata, dependencies, and signature.
/// </summary>
public class ModuleDescriptor
{
    /// <summary>
    /// Unique name of the module (e.g., "payment-processor").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Semantic version of the module (e.g., 2.1.0).
    /// </summary>
    public required Version Version { get; set; }

    /// <summary>
    /// Human-readable description of the module.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Author or team responsible for the module.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Cryptographic signature (RSA-2048 minimum).
    /// </summary>
    public byte[]? Signature { get; set; }

    /// <summary>
    /// Signature algorithm used (default: RS256).
    /// </summary>
    public string SignatureAlgorithm { get; set; } = "RS256";

    /// <summary>
    /// Resource requirements for running the module.
    /// </summary>
    public ResourceRequirements ResourceRequirements { get; set; } = new();

    /// <summary>
    /// Module dependencies with version constraints.
    /// Key: module name, Value: version constraint (e.g., ">=1.0.0").
    /// </summary>
    public Dictionary<string, string> Dependencies { get; set; } = new();

    /// <summary>
    /// Configuration parameters for the module.
    /// </summary>
    public Dictionary<string, string> Configuration { get; set; } = new();

    /// <summary>
    /// Validates the module descriptor.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 100)
        {
            throw new ArgumentException("Name must be between 1-100 characters", nameof(Name));
        }

        if (ResourceRequirements.MemoryMB <= 0)
        {
            throw new ArgumentException("Memory requirement must be positive", nameof(ResourceRequirements));
        }

        if (ResourceRequirements.CpuCores <= 0)
        {
            throw new ArgumentException("CPU requirement must be positive", nameof(ResourceRequirements));
        }
    }
}

/// <summary>
/// Resource requirements for a module.
/// </summary>
public class ResourceRequirements
{
    /// <summary>
    /// Required memory in megabytes.
    /// </summary>
    public int MemoryMB { get; set; } = 512;

    /// <summary>
    /// Required CPU cores.
    /// </summary>
    public double CpuCores { get; set; } = 1.0;

    /// <summary>
    /// Required disk space in megabytes.
    /// </summary>
    public int DiskMB { get; set; } = 100;
}
