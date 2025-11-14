using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Verifies module signatures and performs security validation.
/// </summary>
public interface IModuleVerifier
{
    /// <summary>
    /// Verifies the cryptographic signature of a module.
    /// </summary>
    Task<SignatureValidation> VerifySignatureAsync(
        ModuleDescriptor descriptor,
        byte[] moduleBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs comprehensive validation including signature, integrity, and dependencies.
    /// </summary>
    Task<ModuleValidationResult> ValidateModuleAsync(
        ModuleDescriptor descriptor,
        byte[] moduleBytes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of signature verification.
/// </summary>
public class SignatureValidation
{
    public bool IsValid { get; set; }
    public bool IsSigned { get; set; }
    public bool IsTrusted { get; set; }
    public string? SignerName { get; set; }
    public DateTime? SigningTime { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Comprehensive module validation result.
/// </summary>
public class ModuleValidationResult
{
    public bool IsValid { get; set; }
    public SignatureValidation? SignatureValidation { get; set; }
    public bool IntegrityCheckPassed { get; set; }
    public bool DependenciesResolved { get; set; }
    public List<string> ValidationMessages { get; set; } = new();
}
