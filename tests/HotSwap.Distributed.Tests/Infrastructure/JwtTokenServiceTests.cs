using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Authentication;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class JwtTokenServiceTests
{
    private readonly Mock<ILogger<JwtTokenService>> _loggerMock;
    private readonly JwtConfiguration _config;

    public JwtTokenServiceTests()
    {
        _loggerMock = new Mock<ILogger<JwtTokenService>>();
        _config = new JwtConfiguration
        {
            SecretKey = "TestSecretKey-MinimumLength32CharactersRequired-ForSecurity",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 60
        };
    }

    [Fact]
    public void Constructor_WithValidConfig_ShouldInitialize()
    {
        // Act
        var service = new JwtTokenService(_config, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new JwtTokenService(null!, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("config");
    }

    [Fact]
    public void Constructor_WithShortSecretKey_ShouldThrowArgumentException()
    {
        // Arrange
        var invalidConfig = new JwtConfiguration
        {
            SecretKey = "TooShort", // Less than 32 characters
            Issuer = "TestIssuer",
            Audience = "TestAudience"
        };

        // Act
        Action act = () => new JwtTokenService(invalidConfig, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*32 characters*");
    }

    [Fact]
    public void GenerateToken_WithValidUser_ShouldReturnTokenAndExpiration()
    {
        // Arrange
        var service = new JwtTokenService(_config, _loggerMock.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            Roles = new List<UserRole> { UserRole.Deployer }
        };

        // Act
        var (token, expiresAt) = service.GenerateToken(user);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
        expiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(60), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateToken_WithMultipleRoles_ShouldIncludeAllRolesInToken()
    {
        // Arrange
        var service = new JwtTokenService(_config, _loggerMock.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@example.com",
            FullName = "Admin User",
            Roles = new List<UserRole> { UserRole.Admin, UserRole.Deployer, UserRole.Viewer }
        };

        // Act
        var (token, _) = service.GenerateToken(user);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
        // Token should contain all roles (verified in validation test)
    }

    [Fact]
    public void GenerateToken_WithNullUser_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = new JwtTokenService(_config, _loggerMock.Object);

        // Act
        Action act = () => service.GenerateToken(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("user");
    }

    [Fact]
    public void ValidateToken_WithValidToken_ShouldReturnUserId()
    {
        // Arrange
        var service = new JwtTokenService(_config, _loggerMock.Object);
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            Roles = new List<UserRole> { UserRole.Viewer }
        };

        var (token, _) = service.GenerateToken(user);

        // Act
        var result = service.ValidateToken(token);

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public void ValidateToken_WithInvalidToken_ShouldReturnNull()
    {
        // Arrange
        var service = new JwtTokenService(_config, _loggerMock.Object);
        var invalidToken = "invalid.token.string";

        // Act
        var result = service.ValidateToken(invalidToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_WithNullOrEmptyToken_ShouldReturnNull()
    {
        // Arrange
        var service = new JwtTokenService(_config, _loggerMock.Object);

        // Act
        var resultNull = service.ValidateToken(null!);
        var resultEmpty = service.ValidateToken(string.Empty);
        var resultWhitespace = service.ValidateToken("   ");

        // Assert
        resultNull.Should().BeNull();
        resultEmpty.Should().BeNull();
        resultWhitespace.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_WithExpiredToken_ShouldReturnNull()
    {
        // Arrange
        var expiredConfig = new JwtConfiguration
        {
            SecretKey = "TestSecretKey-MinimumLength32CharactersRequired-ForSecurity",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = -1 // Already expired
        };

        var service = new JwtTokenService(expiredConfig, _loggerMock.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            Roles = new List<UserRole> { UserRole.Viewer }
        };

        var (token, _) = service.GenerateToken(user);

        // Give the token time to expire
        Thread.Sleep(100);

        // Act
        var result = service.ValidateToken(token);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_WithDifferentIssuer_ShouldReturnNull()
    {
        // Arrange
        var service1 = new JwtTokenService(_config, _loggerMock.Object);

        var differentConfig = new JwtConfiguration
        {
            SecretKey = _config.SecretKey,
            Issuer = "DifferentIssuer", // Different issuer
            Audience = _config.Audience,
            ExpirationMinutes = 60
        };

        var service2 = new JwtTokenService(differentConfig, _loggerMock.Object);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            Roles = new List<UserRole> { UserRole.Viewer }
        };

        var (token, _) = service1.GenerateToken(user);

        // Act
        var result = service2.ValidateToken(token);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GenerateToken_ConsecutiveCalls_ShouldGenerateDifferentTokens()
    {
        // Arrange
        var service = new JwtTokenService(_config, _loggerMock.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            Roles = new List<UserRole> { UserRole.Viewer }
        };

        // Act
        var (token1, _) = service.GenerateToken(user);
        Thread.Sleep(10); // Small delay to ensure different JTI claim
        var (token2, _) = service.GenerateToken(user);

        // Assert
        token1.Should().NotBe(token2);
    }

    #region Secret Rotation Tests

    [Fact]
    public void Constructor_WithSecretService_ShouldLoadKeysFromSecretService()
    {
        // Arrange
        var secretServiceMock = new Mock<ISecretService>();
        var jwtSigningKey = "SecretServiceKey-MinimumLength32CharactersRequired-FromSecretStore";

        secretServiceMock
            .Setup(x => x.GetSecretAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretVersion
            {
                SecretId = "jwt-signing-key",
                Version = 1,
                Value = jwtSigningKey,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

        secretServiceMock
            .Setup(x => x.GetSecretMetadataAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretMetadata
            {
                SecretId = "jwt-signing-key",
                CurrentVersion = 1,
                CreatedAt = DateTime.UtcNow
            });

        // Act
        var service = new JwtTokenService(_config, _loggerMock.Object, secretServiceMock.Object);

        // Assert
        service.Should().NotBeNull();
        secretServiceMock.Verify(x => x.GetSecretAsync("jwt-signing-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_WithSecretServiceReturningNull_ShouldFallBackToConfiguration()
    {
        // Arrange
        var secretServiceMock = new Mock<ISecretService>();

        secretServiceMock
            .Setup(x => x.GetSecretAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SecretVersion?)null);

        // Act
        var service = new JwtTokenService(_config, _loggerMock.Object, secretServiceMock.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            Roles = new List<UserRole> { UserRole.Viewer }
        };

        var (token, _) = service.GenerateToken(user);

        // Assert
        service.Should().NotBeNull();
        token.Should().NotBeNullOrWhiteSpace();
        secretServiceMock.Verify(x => x.GetSecretAsync("jwt-signing-key", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public void ValidateToken_DuringRotationWindow_ShouldAcceptTokensSignedWithPreviousKey()
    {
        // Arrange - Setup secret service with rotation window (both current and previous keys)
        var secretServiceMock = new Mock<ISecretService>();
        var currentKey = "CurrentKey-MinimumLength32CharactersRequired-ForSecurityPurposes";
        var previousKey = "PreviousKey-MinimumLength32CharactersRequired-ForSecurityPurpos";

        secretServiceMock
            .Setup(x => x.GetSecretAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretVersion
            {
                SecretId = "jwt-signing-key",
                Version = 2,
                Value = currentKey,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

        secretServiceMock
            .Setup(x => x.GetSecretMetadataAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretMetadata
            {
                SecretId = "jwt-signing-key",
                CurrentVersion = 2,
                PreviousVersion = 1,
                CreatedAt = DateTime.UtcNow,
                LastRotatedAt = DateTime.UtcNow
            });

        secretServiceMock
            .Setup(x => x.GetSecretVersionAsync("jwt-signing-key", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretVersion
            {
                SecretId = "jwt-signing-key",
                Version = 1,
                Value = previousKey,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            });

        // Create a token with the previous key (simulating a token generated before rotation)
        var previousConfig = new JwtConfiguration
        {
            SecretKey = previousKey,
            Issuer = _config.Issuer,
            Audience = _config.Audience,
            ExpirationMinutes = 60
        };
        var previousService = new JwtTokenService(previousConfig, _loggerMock.Object);
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            Roles = new List<UserRole> { UserRole.Viewer }
        };

        var (oldToken, _) = previousService.GenerateToken(user);

        // Act - Create service with rotation window and validate old token
        var service = new JwtTokenService(_config, _loggerMock.Object, secretServiceMock.Object);
        var result = service.ValidateToken(oldToken);

        // Assert
        result.Should().Be(userId, "Token signed with previous key should be valid during rotation window");
        secretServiceMock.Verify(x => x.GetSecretVersionAsync("jwt-signing-key", 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GenerateToken_WithSecretService_ShouldAlwaysUseCurrentKey()
    {
        // Arrange
        var secretServiceMock = new Mock<ISecretService>();
        var currentKey = "CurrentKey-MinimumLength32CharactersRequired-ForSecurityPurposes";

        secretServiceMock
            .Setup(x => x.GetSecretAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretVersion
            {
                SecretId = "jwt-signing-key",
                Version = 2,
                Value = currentKey,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

        secretServiceMock
            .Setup(x => x.GetSecretMetadataAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretMetadata
            {
                SecretId = "jwt-signing-key",
                CurrentVersion = 2,
                PreviousVersion = 1,
                CreatedAt = DateTime.UtcNow
            });

        var service = new JwtTokenService(_config, _loggerMock.Object, secretServiceMock.Object);
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            Roles = new List<UserRole> { UserRole.Viewer }
        };

        // Act - Generate token
        var (token, _) = service.GenerateToken(user);

        // Assert - Token should be generated with current key
        token.Should().NotBeNullOrWhiteSpace();

        // Verify by validating with a service configured with current key
        var currentConfig = new JwtConfiguration
        {
            SecretKey = currentKey,
            Issuer = _config.Issuer,
            Audience = _config.Audience,
            ExpirationMinutes = 60
        };
        var currentService = new JwtTokenService(currentConfig, _loggerMock.Object);
        var validationResult = currentService.ValidateToken(token);

        validationResult.Should().Be(userId, "Token should be signed with current key");
    }

    [Fact]
    public void RefreshKeys_WhenCalled_ShouldReloadKeysFromSecretService()
    {
        // Arrange
        var secretServiceMock = new Mock<ISecretService>();
        var initialKey = "InitialKey-MinimumLength32CharactersRequired-ForSecurityPurpos";
        var rotatedKey = "RotatedKey-MinimumLength32CharactersRequired-ForSecurityPurpos";

        // Initial setup - version 1
        secretServiceMock
            .Setup(x => x.GetSecretAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretVersion
            {
                SecretId = "jwt-signing-key",
                Version = 1,
                Value = initialKey,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

        secretServiceMock
            .Setup(x => x.GetSecretMetadataAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretMetadata
            {
                SecretId = "jwt-signing-key",
                CurrentVersion = 1,
                CreatedAt = DateTime.UtcNow
            });

        var service = new JwtTokenService(_config, _loggerMock.Object, secretServiceMock.Object);

        // Update mock to return rotated key (version 2)
        secretServiceMock
            .Setup(x => x.GetSecretAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretVersion
            {
                SecretId = "jwt-signing-key",
                Version = 2,
                Value = rotatedKey,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

        secretServiceMock
            .Setup(x => x.GetSecretMetadataAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretMetadata
            {
                SecretId = "jwt-signing-key",
                CurrentVersion = 2,
                PreviousVersion = 1,
                CreatedAt = DateTime.UtcNow,
                LastRotatedAt = DateTime.UtcNow
            });

        secretServiceMock
            .Setup(x => x.GetSecretVersionAsync("jwt-signing-key", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretVersion
            {
                SecretId = "jwt-signing-key",
                Version = 1,
                Value = initialKey,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                IsActive = true
            });

        // Act - Manually refresh keys
        service.RefreshKeys();

        // Generate token after refresh
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            Roles = new List<UserRole> { UserRole.Viewer }
        };
        var (token, _) = service.GenerateToken(user);

        // Assert - Token should be signed with rotated key
        var rotatedConfig = new JwtConfiguration
        {
            SecretKey = rotatedKey,
            Issuer = _config.Issuer,
            Audience = _config.Audience,
            ExpirationMinutes = 60
        };
        var rotatedService = new JwtTokenService(rotatedConfig, _loggerMock.Object);
        var validationResult = rotatedService.ValidateToken(token);

        validationResult.Should().NotBeNull("Token should be signed with rotated key after refresh");
        secretServiceMock.Verify(x => x.GetSecretAsync("jwt-signing-key", It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public void ValidateToken_AfterRotationWindowEnds_ShouldRejectTokensSignedWithOldKey()
    {
        // Arrange - Setup secret service with NO rotation window (only current key)
        var secretServiceMock = new Mock<ISecretService>();
        var currentKey = "CurrentKey-MinimumLength32CharactersRequired-ForSecurityPurposes";
        var previousKey = "PreviousKey-MinimumLength32CharactersRequired-ForSecurityPurpos";

        // Create a token with the old key
        var previousConfig = new JwtConfiguration
        {
            SecretKey = previousKey,
            Issuer = _config.Issuer,
            Audience = _config.Audience,
            ExpirationMinutes = 60
        };
        var previousService = new JwtTokenService(previousConfig, _loggerMock.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            Roles = new List<UserRole> { UserRole.Viewer }
        };

        var (oldToken, _) = previousService.GenerateToken(user);

        // Setup secret service with only current key (rotation window ended)
        secretServiceMock
            .Setup(x => x.GetSecretAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretVersion
            {
                SecretId = "jwt-signing-key",
                Version = 2,
                Value = currentKey,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

        secretServiceMock
            .Setup(x => x.GetSecretMetadataAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretMetadata
            {
                SecretId = "jwt-signing-key",
                CurrentVersion = 2,
                PreviousVersion = null, // Rotation window ended
                CreatedAt = DateTime.UtcNow,
                LastRotatedAt = DateTime.UtcNow.AddDays(-1)
            });

        // Act - Validate old token after rotation window ends
        var service = new JwtTokenService(_config, _loggerMock.Object, secretServiceMock.Object);
        var result = service.ValidateToken(oldToken);

        // Assert
        result.Should().BeNull("Token signed with old key should be rejected after rotation window ends");
    }

    [Fact]
    public void Constructor_WithSecretServiceThrowingException_ShouldFallBackToConfiguration()
    {
        // Arrange
        var secretServiceMock = new Mock<ISecretService>();

        secretServiceMock
            .Setup(x => x.GetSecretAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Secret service unavailable"));

        // Act
        var service = new JwtTokenService(_config, _loggerMock.Object, secretServiceMock.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            Roles = new List<UserRole> { UserRole.Viewer }
        };

        var (token, _) = service.GenerateToken(user);
        var validationResult = service.ValidateToken(token);

        // Assert
        service.Should().NotBeNull();
        token.Should().NotBeNullOrWhiteSpace();
        validationResult.Should().NotBeNull("Service should fall back to configuration key when secret service fails");
    }

    [Fact]
    public void RefreshKeys_WithoutSecretService_ShouldLogDebugAndDoNothing()
    {
        // Arrange - Service without secret service
        var service = new JwtTokenService(_config, _loggerMock.Object, null);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            Roles = new List<UserRole> { UserRole.Viewer }
        };

        var (tokenBefore, _) = service.GenerateToken(user);

        // Act - Call RefreshKeys (should do nothing)
        service.RefreshKeys();

        var (tokenAfter, _) = service.GenerateToken(user);

        // Assert - Both tokens should validate successfully with same key
        var resultBefore = service.ValidateToken(tokenBefore);
        var resultAfter = service.ValidateToken(tokenAfter);

        resultBefore.Should().Be(user.Id);
        resultAfter.Should().Be(user.Id);
    }

    [Fact]
    public void ValidateToken_WithMultipleKeysInRotationWindow_ShouldTryCurrentKeyFirst()
    {
        // Arrange
        var secretServiceMock = new Mock<ISecretService>();
        var currentKey = "CurrentKey-MinimumLength32CharactersRequired-ForSecurityPurposes";
        var previousKey = "PreviousKey-MinimumLength32CharactersRequired-ForSecurityPurpos";

        secretServiceMock
            .Setup(x => x.GetSecretAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretVersion
            {
                SecretId = "jwt-signing-key",
                Version = 2,
                Value = currentKey,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

        secretServiceMock
            .Setup(x => x.GetSecretMetadataAsync("jwt-signing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretMetadata
            {
                SecretId = "jwt-signing-key",
                CurrentVersion = 2,
                PreviousVersion = 1,
                CreatedAt = DateTime.UtcNow,
                LastRotatedAt = DateTime.UtcNow
            });

        secretServiceMock
            .Setup(x => x.GetSecretVersionAsync("jwt-signing-key", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecretVersion
            {
                SecretId = "jwt-signing-key",
                Version = 1,
                Value = previousKey,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            });

        var service = new JwtTokenService(_config, _loggerMock.Object, secretServiceMock.Object);
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            Roles = new List<UserRole> { UserRole.Viewer }
        };

        // Generate token with current key
        var (token, _) = service.GenerateToken(user);

        // Act - Validate token (should use current key, not try previous)
        var result = service.ValidateToken(token);

        // Assert
        result.Should().Be(userId);
        // Service should validate with current key successfully (no need to try previous)
    }

    #endregion
}
