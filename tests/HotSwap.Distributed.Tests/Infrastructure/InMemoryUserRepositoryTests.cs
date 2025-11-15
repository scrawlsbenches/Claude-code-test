using FluentAssertions;
using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Authentication;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class InMemoryUserRepositoryTests
{
    private readonly Mock<ILogger<InMemoryUserRepository>> _loggerMock;

    public InMemoryUserRepositoryTests()
    {
        _loggerMock = new Mock<ILogger<InMemoryUserRepository>>();
    }

    [Fact]
    public async Task Constructor_ShouldInitializeDemoUsers()
    {
        // Act
        var repository = new InMemoryUserRepository(_loggerMock.Object);
        var users = await repository.GetAllAsync();

        // Assert
        users.Should().HaveCount(3);
        users.Should().Contain(u => u.Username == "admin");
        users.Should().Contain(u => u.Username == "deployer");
        users.Should().Contain(u => u.Username == "viewer");
    }

    [Fact]
    public async Task FindByUsernameAsync_WithExistingUsername_ShouldReturnUser()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);

        // Act
        var user = await repository.FindByUsernameAsync("admin");

        // Assert
        user.Should().NotBeNull();
        user!.Username.Should().Be("admin");
        user.Email.Should().Be("admin@example.com");
        user.Roles.Should().Contain(UserRole.Admin);
    }

    [Fact]
    public async Task FindByUsernameAsync_WithNonExistingUsername_ShouldReturnNull()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);

        // Act
        var user = await repository.FindByUsernameAsync("nonexistent");

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task FindByUsernameAsync_IsCaseInsensitive()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);

        // Act
        var user1 = await repository.FindByUsernameAsync("ADMIN");
        var user2 = await repository.FindByUsernameAsync("admin");
        var user3 = await repository.FindByUsernameAsync("AdMiN");

        // Assert
        user1.Should().NotBeNull();
        user2.Should().NotBeNull();
        user3.Should().NotBeNull();
        user1!.Id.Should().Be(user2!.Id).And.Be(user3!.Id);
    }

    [Fact]
    public async Task FindByIdAsync_WithExistingId_ShouldReturnUser()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);
        var adminId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // Act
        var user = await repository.FindByIdAsync(adminId);

        // Assert
        user.Should().NotBeNull();
        user!.Username.Should().Be("admin");
    }

    [Fact]
    public async Task FindByIdAsync_WithNonExistingId_ShouldReturnNull()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);
        var randomId = Guid.NewGuid();

        // Act
        var user = await repository.FindByIdAsync(randomId);

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WithNewUser_ShouldAddUser()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);
        var newUser = new User
        {
            Username = "newuser",
            Email = "newuser@example.com",
            FullName = "New User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Roles = new List<UserRole> { UserRole.Viewer }
        };

        // Act
        var created = await repository.CreateAsync(newUser);

        // Assert
        created.Should().NotBeNull();
        created.Id.Should().NotBeEmpty();
        created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var found = await repository.FindByUsernameAsync("newuser");
        found.Should().NotBeNull();
        found!.Email.Should().Be("newuser@example.com");
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateUsername_ShouldThrowException()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);
        var duplicateUser = new User
        {
            Username = "admin", // Already exists
            Email = "duplicate@example.com",
            FullName = "Duplicate User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Roles = new List<UserRole> { UserRole.Viewer }
        };

        // Act
        Func<Task> act = async () => await repository.CreateAsync(duplicateUser);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateAsync_WithExistingUser_ShouldUpdateUser()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);
        var user = await repository.FindByUsernameAsync("viewer");
        user!.FullName = "Updated Name";

        // Act
        var updated = await repository.UpdateAsync(user);

        // Assert
        updated.FullName.Should().Be("Updated Name");

        var found = await repository.FindByIdAsync(user.Id);
        found!.FullName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistingUser_ShouldThrowException()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);
        var nonExistingUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "nonexisting",
            Email = "nonexisting@example.com",
            FullName = "Non Existing",
            PasswordHash = "hash",
            Roles = new List<UserRole>()
        };

        // Act
        Func<Task> act = async () => await repository.UpdateAsync(nonExistingUser);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithValidCredentials_ShouldReturnUser()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);

        // Act
        var user = await repository.ValidateCredentialsAsync("admin", "Admin123!");

        // Assert
        user.Should().NotBeNull();
        user!.Username.Should().Be("admin");
        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithInvalidPassword_ShouldReturnNull()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);

        // Act
        var user = await repository.ValidateCredentialsAsync("admin", "WrongPassword");

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithNonExistingUsername_ShouldReturnNull()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);

        // Act
        var user = await repository.ValidateCredentialsAsync("nonexistent", "password");

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithInactiveUser_ShouldReturnNull()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);
        var user = await repository.FindByUsernameAsync("viewer");
        user!.IsActive = false;
        await repository.UpdateAsync(user);

        // Act
        var result = await repository.ValidateCredentialsAsync("viewer", "Viewer123!");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("admin", "Admin123!", UserRole.Admin)]
    [InlineData("deployer", "Deploy123!", UserRole.Deployer)]
    [InlineData("viewer", "Viewer123!", UserRole.Viewer)]
    public async Task DemoUsers_ShouldHaveCorrectRoles(string username, string password, UserRole expectedRole)
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);

        // Act
        var user = await repository.ValidateCredentialsAsync(username, password);

        // Assert
        user.Should().NotBeNull();
        user!.Roles.Should().Contain(expectedRole);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllUsers()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);

        // Act
        var users = await repository.GetAllAsync();

        // Assert
        users.Should().HaveCountGreaterOrEqualTo(3); // At least the 3 demo users
    }

    [Fact]
    public async Task ValidateCredentialsAsync_UpdatesLastLoginTime()
    {
        // Arrange
        var repository = new InMemoryUserRepository(_loggerMock.Object);
        var beforeLogin = DateTime.UtcNow;

        // Act
        await repository.ValidateCredentialsAsync("admin", "Admin123!");
        var user = await repository.FindByUsernameAsync("admin");

        // Assert
        user!.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt.Should().BeOnOrAfter(beforeLogin);
    }
}
