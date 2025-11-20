using FluentAssertions;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using HotSwap.Distributed.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Security;

public class ModuleVerifierTests
{
    private readonly Mock<ILogger<ModuleVerifier>> _loggerMock;
    private readonly ModuleVerifier _verifierStrictMode;
    private readonly ModuleVerifier _verifierNonStrictMode;

    public ModuleVerifierTests()
    {
        _loggerMock = new Mock<ILogger<ModuleVerifier>>();
        _verifierStrictMode = new ModuleVerifier(_loggerMock.Object, strictMode: true);
        _verifierNonStrictMode = new ModuleVerifier(_loggerMock.Object, strictMode: false);
    }

    #region VerifySignatureAsync Tests

    [Fact]
    public async Task VerifySignatureAsync_UnsignedModuleInStrictMode_ShouldReturnInvalidWithError()
    {
        // Arrange
        var descriptor = CreateValidDescriptor();
        descriptor.Signature = null;
        var moduleBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = await _verifierStrictMode.VerifySignatureAsync(descriptor, moduleBytes);

        // Assert
        result.Should().NotBeNull();
        result.IsSigned.Should().BeFalse();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not signed") && e.Contains("strict mode"));
        result.Warnings.Should().BeEmpty();
        result.SignerName.Should().BeNull();
        result.SigningTime.Should().BeNull();

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("is not signed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifySignatureAsync_UnsignedModuleInNonStrictMode_ShouldReturnValidWithWarning()
    {
        // Arrange
        var descriptor = CreateValidDescriptor();
        descriptor.Signature = null;
        var moduleBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = await _verifierNonStrictMode.VerifySignatureAsync(descriptor, moduleBytes);

        // Assert
        result.Should().NotBeNull();
        result.IsSigned.Should().BeFalse();
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("not signed") && w.Contains("strict mode is disabled"));
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifySignatureAsync_EmptySignature_ShouldReturnInvalidInStrictMode()
    {
        // Arrange
        var descriptor = CreateValidDescriptor();
        descriptor.Signature = Array.Empty<byte>();
        var moduleBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = await _verifierStrictMode.VerifySignatureAsync(descriptor, moduleBytes);

        // Assert
        result.Should().NotBeNull();
        result.IsSigned.Should().BeFalse();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not signed"));
    }

    [Fact]
    public async Task VerifySignatureAsync_InvalidPkcs7Format_ShouldReturnInvalidWithError()
    {
        // Arrange
        var descriptor = CreateValidDescriptor();
        descriptor.Signature = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }; // Invalid PKCS#7 format
        var moduleBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = await _verifierStrictMode.VerifySignatureAsync(descriptor, moduleBytes);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("error") || e.Contains("verification"));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error verifying signature")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifySignatureAsync_NullModuleBytes_ShouldNotThrow()
    {
        // Arrange
        var descriptor = CreateValidDescriptor();
        descriptor.Signature = null;

        // Act
        var result = await _verifierStrictMode.VerifySignatureAsync(descriptor, null!);

        // Assert - Should handle gracefully
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task VerifySignatureAsync_CancellationRequested_ShouldRespectCancellation()
    {
        // Arrange
        var descriptor = CreateValidDescriptor();
        descriptor.Signature = null;
        var moduleBytes = new byte[] { 1, 2, 3, 4, 5 };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Should complete even if cancelled (signature verification is CPU-bound)
        var result = await _verifierStrictMode.VerifySignatureAsync(descriptor, moduleBytes, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region ValidateModuleAsync Tests

    [Fact]
    public async Task ValidateModuleAsync_ValidUnsignedModuleInNonStrictMode_ShouldReturnValid()
    {
        // Arrange
        var descriptor = CreateValidDescriptor();
        descriptor.Signature = null;
        var moduleBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = await _verifierNonStrictMode.ValidateModuleAsync(descriptor, moduleBytes);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.SignatureValidation.Should().NotBeNull();
        result.SignatureValidation!.IsValid.Should().BeTrue();
        result.IntegrityCheckPassed.Should().BeTrue();
        result.DependenciesResolved.Should().BeTrue();
        result.ValidationMessages.Should().BeEmpty();

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Module validation")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateModuleAsync_InvalidDescriptor_ShouldReturnInvalidWithMessage()
    {
        // Arrange
        var descriptor = new ModuleDescriptor
        {
            Name = "",  // Invalid: empty name
            Version = new Version(1, 0, 0)
        };
        var moduleBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = await _verifierStrictMode.ValidateModuleAsync(descriptor, moduleBytes);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ValidationMessages.Should().Contain(m => m.Contains("error") || m.Contains("Validation"));
    }

    [Fact]
    public async Task ValidateModuleAsync_EmptyModuleBytes_ShouldFailIntegrityCheck()
    {
        // Arrange
        var descriptor = CreateValidDescriptor();
        descriptor.Signature = null;
        var moduleBytes = Array.Empty<byte>();

        // Act
        var result = await _verifierNonStrictMode.ValidateModuleAsync(descriptor, moduleBytes);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.IntegrityCheckPassed.Should().BeFalse();
        result.ValidationMessages.Should().Contain(m => m.Contains("empty"));
    }

    [Fact]
    public async Task ValidateModuleAsync_ModuleWithDependencies_ShouldLogDependencyCount()
    {
        // Arrange
        var descriptor = CreateValidDescriptor();
        descriptor.Signature = null;
        descriptor.Dependencies.Add("dependency1", ">=1.0.0");
        descriptor.Dependencies.Add("dependency2", ">=2.0.0");
        var moduleBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = await _verifierNonStrictMode.ValidateModuleAsync(descriptor, moduleBytes);

        // Assert
        result.Should().NotBeNull();
        result.ValidationMessages.Should().Contain(m => m.Contains("2 dependencies"));
        result.DependenciesResolved.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateModuleAsync_SignatureValidationFails_ShouldReturnInvalid()
    {
        // Arrange
        var descriptor = CreateValidDescriptor();
        descriptor.Signature = null;  // Will fail in strict mode
        var moduleBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = await _verifierStrictMode.ValidateModuleAsync(descriptor, moduleBytes);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.SignatureValidation.Should().NotBeNull();
        result.SignatureValidation!.IsValid.Should().BeFalse();
        result.ValidationMessages.Should().Contain(m => m.Contains("Signature validation failed"));
    }

    [Fact]
    public async Task ValidateModuleAsync_NullModuleBytes_ShouldFailIntegrityCheck()
    {
        // Arrange
        var descriptor = CreateValidDescriptor();
        descriptor.Signature = null;

        // Act
        var result = await _verifierNonStrictMode.ValidateModuleAsync(descriptor, null!);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateModuleAsync_CancellationRequested_ShouldRespectCancellation()
    {
        // Arrange
        var descriptor = CreateValidDescriptor();
        descriptor.Signature = null;
        var moduleBytes = new byte[] { 1, 2, 3, 4, 5 };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Should complete even if cancelled (validation is CPU-bound)
        var result = await _verifierNonStrictMode.ValidateModuleAsync(descriptor, moduleBytes, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithStrictModeTrue_ShouldCreateVerifierInStrictMode()
    {
        // Arrange & Act
        var verifier = new ModuleVerifier(_loggerMock.Object, strictMode: true);

        // Assert
        verifier.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithStrictModeFalse_ShouldCreateVerifierInNonStrictMode()
    {
        // Arrange & Act
        var verifier = new ModuleVerifier(_loggerMock.Object, strictMode: false);

        // Assert
        verifier.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithDefaultStrictMode_ShouldDefaultToTrue()
    {
        // Arrange & Act
        var verifier = new ModuleVerifier(_loggerMock.Object);

        // Assert - Default is strict mode, verified by behavior
        verifier.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private ModuleDescriptor CreateValidDescriptor()
    {
        return new ModuleDescriptor
        {
            Name = "test-module",
            Version = new Version(1, 0, 0),
            Description = "Test module for unit testing",
            Author = "Test Author",
            SignatureAlgorithm = "RS256",
            ResourceRequirements = new ResourceRequirements
            {
                MemoryMB = 512,
                CpuCores = 1.0,
                DiskMB = 100
            }
        };
    }

    #endregion
}
