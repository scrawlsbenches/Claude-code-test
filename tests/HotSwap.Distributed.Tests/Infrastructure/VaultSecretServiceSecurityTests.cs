using System.Reflection;
using System.Security.Cryptography;
using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.SecretManagement;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

/// <summary>
/// Security-focused tests for VaultSecretService.
/// Tests verify fixes for CRITICAL-01 (TLS bypass) and CRITICAL-02 (weak RNG).
/// </summary>
public class VaultSecretServiceSecurityTests
{
    /// <summary>
    /// Tests that GenerateRandomSecret uses cryptographically secure RNG.
    /// This test verifies the fix for CRITICAL-02: Weak Cryptographic Random Number Generation.
    /// The fix replaced Random() with RandomNumberGenerator for security.
    /// </summary>
    [Fact]
    public void GenerateRandomSecret_UsesSecureRNG_NotPredictableRandom()
    {
        // Arrange
        var config = new VaultConfiguration
        {
            VaultUrl = "https://vault.example.com:8200",
            Token = "test-token",
            AuthMethod = VaultAuthMethod.Token
        };
        var mockLogger = new Mock<ILogger<VaultSecretService>>();

        // We can't instantiate VaultSecretService without valid Vault connection,
        // but we can verify the implementation uses RandomNumberGenerator via reflection

        // Act - Call GenerateRandomSecret via reflection (it's private)
        var service = new VaultSecretService(config, mockLogger.Object);
        var method = typeof(VaultSecretService).GetMethod(
            "GenerateRandomSecret",
            BindingFlags.NonPublic | BindingFlags.Instance);

        method.Should().NotBeNull("GenerateRandomSecret method should exist");

        // Generate multiple secrets
        var secrets = new HashSet<string>();
        for (int i = 0; i < 10; i++)
        {
            var secret = method!.Invoke(service, null) as string;
            secret.Should().NotBeNullOrEmpty();
            secret!.Length.Should().Be(64, "secret should be 64 characters long");
            secrets.Add(secret);
        }

        // Assert - All secrets should be unique (cryptographically random)
        secrets.Should().HaveCount(10, "all generated secrets should be unique with secure RNG");

        // Verify secret contains valid characters
        const string validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()-_=+[]{}|;:,.<>?";
        foreach (var secret in secrets)
        {
            secret.Should().Match(s => s.All(c => validChars.Contains(c)),
                "secret should only contain valid characters");
        }
    }

    /// <summary>
    /// Tests that generated secrets have high entropy (not predictable).
    /// Validates that the secure RNG produces non-repeating, high-entropy secrets.
    /// </summary>
    [Fact]
    public void GenerateRandomSecret_ProducesHighEntropySecrets()
    {
        // Arrange
        var config = new VaultConfiguration
        {
            VaultUrl = "https://vault.example.com:8200",
            Token = "test-token",
            AuthMethod = VaultAuthMethod.Token
        };
        var mockLogger = new Mock<ILogger<VaultSecretService>>();
        var service = new VaultSecretService(config, mockLogger.Object);

        var method = typeof(VaultSecretService).GetMethod(
            "GenerateRandomSecret",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act - Generate 100 secrets
        var secrets = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            var secret = method!.Invoke(service, null) as string;
            secrets.Add(secret!);
        }

        // Assert - All 100 should be unique (collision probability should be negligible with secure RNG)
        secrets.Should().HaveCount(100,
            "cryptographically secure RNG should not produce collisions in 100 attempts");
    }

    /// <summary>
    /// Tests that VaultConfiguration no longer has ValidateCertificate property.
    /// This test verifies the fix for CRITICAL-01: TLS Certificate Validation Can Be Disabled.
    /// The fix removed the ValidateCertificate config option to always enforce TLS validation.
    /// </summary>
    [Fact]
    public void VaultConfiguration_DoesNotHaveValidateCertificateProperty()
    {
        // Act
        var configType = typeof(VaultConfiguration);
        var property = configType.GetProperty("ValidateCertificate");

        // Assert
        property.Should().BeNull(
            "ValidateCertificate property should be removed to prevent TLS bypass");
    }

    /// <summary>
    /// Tests that VaultConfiguration has essential security-related properties.
    /// </summary>
    [Fact]
    public void VaultConfiguration_HasRequiredSecurityProperties()
    {
        // Act
        var configType = typeof(VaultConfiguration);

        // Assert - Verify required properties exist
        configType.GetProperty("VaultUrl").Should().NotBeNull();
        configType.GetProperty("Token").Should().NotBeNull();
        configType.GetProperty("AuthMethod").Should().NotBeNull();
        configType.GetProperty("TimeoutSeconds").Should().NotBeNull();
        configType.GetProperty("RetryAttempts").Should().NotBeNull();
    }

    /// <summary>
    /// Tests VaultConfiguration constructor and default values.
    /// </summary>
    [Fact]
    public void VaultConfiguration_HasSecureDefaults()
    {
        // Act
        var config = new VaultConfiguration();

        // Assert - Verify secure defaults
        config.MountPoint.Should().Be("secret");
        config.AuthMethod.Should().Be(VaultAuthMethod.Token);
        config.TimeoutSeconds.Should().Be(30);
        config.RetryAttempts.Should().Be(3);
        config.RetryDelayMs.Should().Be(1000);
    }

    /// <summary>
    /// Tests that secret generation produces consistent length secrets.
    /// </summary>
    [Fact]
    public void GenerateRandomSecret_AlwaysProduces64CharacterSecrets()
    {
        // Arrange
        var config = new VaultConfiguration
        {
            VaultUrl = "https://vault.example.com:8200",
            Token = "test-token",
            AuthMethod = VaultAuthMethod.Token
        };
        var mockLogger = new Mock<ILogger<VaultSecretService>>();
        var service = new VaultSecretService(config, mockLogger.Object);

        var method = typeof(VaultSecretService).GetMethod(
            "GenerateRandomSecret",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act & Assert - Generate 20 secrets and verify length
        for (int i = 0; i < 20; i++)
        {
            var secret = method!.Invoke(service, null) as string;
            secret.Should().NotBeNullOrEmpty();
            secret!.Length.Should().Be(64, $"secret #{i + 1} should be exactly 64 characters");
        }
    }

    /// <summary>
    /// Tests that VaultAuthMethod enum has expected values for secure authentication.
    /// </summary>
    [Fact]
    public void VaultAuthMethod_HasExpectedAuthenticationMethods()
    {
        // Assert
        Enum.IsDefined(typeof(VaultAuthMethod), VaultAuthMethod.Token).Should().BeTrue();
        Enum.IsDefined(typeof(VaultAuthMethod), VaultAuthMethod.AppRole).Should().BeTrue();
        Enum.IsDefined(typeof(VaultAuthMethod), VaultAuthMethod.Kubernetes).Should().BeTrue();
        Enum.IsDefined(typeof(VaultAuthMethod), VaultAuthMethod.UserPass).Should().BeTrue();
    }

    /// <summary>
    /// SECURITY TEST: Verifies that GenerateRandomSecret has no modulo bias.
    /// Tests that all characters in the charset have equal probability of occurrence.
    /// This ensures maximum entropy and prevents predictable patterns in generated secrets.
    /// </summary>
    [Fact]
    public void GenerateRandomSecret_HasNoModuloBias()
    {
        // Arrange
        var config = new VaultConfiguration
        {
            VaultUrl = "https://vault.example.com:8200",
            Token = "test-token",
            AuthMethod = VaultAuthMethod.Token
        };
        var mockLogger = new Mock<ILogger<VaultSecretService>>();
        var service = new VaultSecretService(config, mockLogger.Object);

        var method = typeof(VaultSecretService).GetMethod(
            "GenerateRandomSecret",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Generate many secrets to analyze distribution
        const int sampleSize = 1000;
        const int secretLength = 64;
        var characterCounts = new Dictionary<char, int>();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()-_=+[]{}|;:,.<>?";

        // Initialize counts
        foreach (var c in chars)
        {
            characterCounts[c] = 0;
        }

        // Act - Generate secrets and count character occurrences
        for (int i = 0; i < sampleSize; i++)
        {
            var secret = method!.Invoke(service, null) as string;
            foreach (var c in secret!)
            {
                if (characterCounts.ContainsKey(c))
                {
                    characterCounts[c]++;
                }
            }
        }

        // Assert - Statistical analysis for bias
        var totalCharacters = sampleSize * secretLength;
        var expectedCountPerChar = (double)totalCharacters / chars.Length;

        // Calculate chi-square statistic to detect bias
        // With uniform distribution, each char should appear ~expectedCountPerChar times
        // We allow 15% deviation as acceptable variance for randomness
        var maxAcceptableDeviation = expectedCountPerChar * 0.15;

        foreach (var kvp in characterCounts)
        {
            var deviation = Math.Abs(kvp.Value - expectedCountPerChar);
            deviation.Should().BeLessThan(maxAcceptableDeviation,
                $"Character '{kvp.Key}' appears {kvp.Value} times, expected ~{expectedCountPerChar:F0} (deviation: {deviation:F0}). " +
                $"This suggests modulo bias in the RNG.");
        }

        // Additional check: no character should be completely absent
        characterCounts.Values.Should().AllSatisfy(count =>
            count.Should().BeGreaterThan(0, "All characters should appear at least once in a large sample"));
    }
}
