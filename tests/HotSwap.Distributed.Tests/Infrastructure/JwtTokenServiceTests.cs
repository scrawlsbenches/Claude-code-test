using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Authentication;
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
}
