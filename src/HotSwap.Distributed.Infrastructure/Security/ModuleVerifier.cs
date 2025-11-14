using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Security;

/// <summary>
/// Verifies module signatures using RSA and X.509 certificates.
/// </summary>
public class ModuleVerifier : IModuleVerifier
{
    private readonly ILogger<ModuleVerifier> _logger;
    private readonly bool _strictMode;

    public ModuleVerifier(ILogger<ModuleVerifier> logger, bool strictMode = true)
    {
        _logger = logger;
        _strictMode = strictMode;
    }

    /// <summary>
    /// Verifies the cryptographic signature of a module.
    /// </summary>
    public async Task<SignatureValidation> VerifySignatureAsync(
        ModuleDescriptor descriptor,
        byte[] moduleBytes,
        CancellationToken cancellationToken = default)
    {
        var result = new SignatureValidation();

        try
        {
            // Check if module is signed
            if (descriptor.Signature == null || descriptor.Signature.Length == 0)
            {
                result.IsSigned = false;
                result.IsValid = !_strictMode;

                if (_strictMode)
                {
                    result.Errors.Add("Module is not signed and strict mode is enabled");
                    _logger.LogWarning("Module {ModuleName} v{Version} is not signed",
                        descriptor.Name, descriptor.Version);
                }
                else
                {
                    result.Warnings.Add("Module is not signed but strict mode is disabled");
                }

                return result;
            }

            result.IsSigned = true;

            // Parse PKCS#7 signature
            var signedCms = new SignedCms();
            signedCms.Decode(descriptor.Signature);

            // Get signer certificate
            if (signedCms.SignerInfos.Count == 0)
            {
                result.IsValid = false;
                result.Errors.Add("No signer information found in signature");
                return result;
            }

            var signerInfo = signedCms.SignerInfos[0];
            var certificate = signerInfo.Certificate;

            if (certificate == null)
            {
                result.IsValid = false;
                result.Errors.Add("Certificate not found in signature");
                return result;
            }

            result.SignerName = certificate.Subject;
            result.SigningTime = GetSigningTime(signerInfo);

            // Check certificate validity
            var now = DateTime.UtcNow;
            if (now < certificate.NotBefore || now > certificate.NotAfter)
            {
                result.IsValid = false;
                result.Errors.Add($"Certificate is not valid (valid from {certificate.NotBefore} to {certificate.NotAfter})");
                _logger.LogError("Certificate for {ModuleName} is expired or not yet valid", descriptor.Name);
                return result;
            }

            // Compute module hash
            byte[] hash;
            using (var sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(moduleBytes);
            }

            // Verify signature (this validates the signature matches the hash)
            try
            {
                // Create ContentInfo with the hash
                var content = new ContentInfo(hash);

                // Create a new SignedCms with the content for verification
                var verifyCms = new SignedCms(content, true); // true = detached signature
                verifyCms.Decode(descriptor.Signature);
                verifyCms.CheckSignature(true); // Verify signature

                result.IsValid = true;
                _logger.LogInformation("Signature verified successfully for {ModuleName} v{Version}",
                    descriptor.Name, descriptor.Version);
            }
            catch (CryptographicException ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Signature verification failed: {ex.Message}");
                _logger.LogError(ex, "Signature verification failed for {ModuleName}", descriptor.Name);
                return result;
            }

            // Check certificate trust
            result.IsTrusted = await CheckCertificateTrustAsync(certificate, cancellationToken);

            if (!result.IsTrusted && _strictMode)
            {
                result.IsValid = false;
                result.Errors.Add("Certificate is not trusted and strict mode is enabled");
                _logger.LogWarning("Certificate for {ModuleName} is not trusted", descriptor.Name);
            }
            else if (!result.IsTrusted)
            {
                result.Warnings.Add("Certificate is not trusted but strict mode is disabled");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying signature for {ModuleName}", descriptor.Name);
            result.IsValid = false;
            result.Errors.Add($"Signature verification error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Performs comprehensive module validation.
    /// </summary>
    public async Task<ModuleValidationResult> ValidateModuleAsync(
        ModuleDescriptor descriptor,
        byte[] moduleBytes,
        CancellationToken cancellationToken = default)
    {
        var result = new ModuleValidationResult
        {
            IsValid = true
        };

        try
        {
            // Validate descriptor
            descriptor.Validate();

            // Verify signature
            result.SignatureValidation = await VerifySignatureAsync(descriptor, moduleBytes, cancellationToken);

            if (!result.SignatureValidation.IsValid)
            {
                result.IsValid = false;
                result.ValidationMessages.Add("Signature validation failed");
            }

            // Integrity check
            result.IntegrityCheckPassed = moduleBytes.Length > 0;

            if (!result.IntegrityCheckPassed)
            {
                result.IsValid = false;
                result.ValidationMessages.Add("Module data is empty");
            }

            // Dependencies check (simplified - would normally check against registry)
            result.DependenciesResolved = true;

            if (descriptor.Dependencies.Count > 0)
            {
                result.ValidationMessages.Add($"Module has {descriptor.Dependencies.Count} dependencies (not validated)");
            }

            _logger.LogInformation("Module validation for {ModuleName} v{Version}: {IsValid}",
                descriptor.Name, descriptor.Version, result.IsValid);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating module {ModuleName}", descriptor.Name);
            result.IsValid = false;
            result.ValidationMessages.Add($"Validation error: {ex.Message}");
            return result;
        }
    }

    private DateTime? GetSigningTime(SignerInfo signerInfo)
    {
        try
        {
            var signingTimeAttr = signerInfo.SignedAttributes
                .Cast<CryptographicAttributeObject>()
                .FirstOrDefault(a => a.Oid?.Value == "1.2.840.113549.1.9.5"); // Signing time OID

            if (signingTimeAttr != null && signingTimeAttr.Values.Count > 0)
            {
                var pkcs9SigningTime = new Pkcs9SigningTime(signingTimeAttr.Values[0].RawData);
                return pkcs9SigningTime.SigningTime;
            }
        }
        catch
        {
            // Ignore errors getting signing time
        }

        return null;
    }

    private async Task<bool> CheckCertificateTrustAsync(
        X509Certificate2 certificate,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

                var isValid = chain.Build(certificate);

                if (!isValid)
                {
                    foreach (var status in chain.ChainStatus)
                    {
                        _logger.LogWarning("Certificate chain status: {Status} - {StatusInfo}",
                            status.Status, status.StatusInformation);
                    }
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking certificate trust");
                return false;
            }
        }, cancellationToken);
    }
}
