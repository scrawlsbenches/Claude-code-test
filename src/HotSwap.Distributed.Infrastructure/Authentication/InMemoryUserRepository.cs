using HotSwap.Distributed.Domain.Enums;
using HotSwap.Distributed.Domain.Models;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotSwap.Distributed.Infrastructure.Authentication;

/// <summary>
/// In-memory implementation of user repository for development and testing.
/// In production, replace with a database-backed implementation.
/// </summary>
public class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, User> _users = new();
    private readonly ILogger<InMemoryUserRepository> _logger;
    private readonly object _lock = new();

    public InMemoryUserRepository(ILogger<InMemoryUserRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        InitializeDemoUsers();
    }

    /// <summary>
    /// Initializes demo users for testing and development.
    /// SECURITY: In production, users should be stored in a secure database.
    /// </summary>
    private void InitializeDemoUsers()
    {
        // SECURITY: Prevent demo users from being initialized in production
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Demo users disabled in production environment. Configure a database-backed user repository.");
            return;
        }

        var demoUsers = new[]
        {
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Username = "admin",
                Email = "admin@example.com",
                FullName = "System Administrator",
                // Password: "Admin123!" - BCrypt hash
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Roles = new List<UserRole> { UserRole.Admin, UserRole.Deployer, UserRole.Viewer },
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Username = "deployer",
                Email = "deployer@example.com",
                FullName = "Deployment Engineer",
                // Password: "Deploy123!" - BCrypt hash
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Deploy123!"),
                Roles = new List<UserRole> { UserRole.Deployer, UserRole.Viewer },
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Username = "viewer",
                Email = "viewer@example.com",
                FullName = "Read-Only User",
                // Password: "Viewer123!" - BCrypt hash
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Viewer123!"),
                Roles = new List<UserRole> { UserRole.Viewer },
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        foreach (var user in demoUsers)
        {
            _users[user.Id] = user;
            _logger.LogInformation("Initialized demo user: {Username} with roles: {Roles}",
                user.Username, string.Join(", ", user.Roles));
        }

        _logger.LogWarning("Using in-memory user repository with demo credentials. " +
                          "Replace with database-backed repository for production use.");
    }

    public Task<User?> FindByUsernameAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return Task.FromResult<User?>(null);

        lock (_lock)
        {
            var user = _users.Values.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(user);
        }
    }

    public Task<User?> FindByIdAsync(Guid userId)
    {
        lock (_lock)
        {
            _users.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }
    }

    public Task<User> CreateAsync(User user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        lock (_lock)
        {
            if (user.Id == Guid.Empty)
                user.Id = Guid.NewGuid();

            if (_users.Values.Any(u => u.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"User with username '{user.Username}' already exists");

            user.CreatedAt = DateTime.UtcNow;
            _users[user.Id] = user;

            _logger.LogInformation("Created user: {Username} (ID: {UserId})", user.Username, user.Id);
            return Task.FromResult(user);
        }
    }

    public Task<User> UpdateAsync(User user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        lock (_lock)
        {
            if (!_users.ContainsKey(user.Id))
                throw new KeyNotFoundException($"User with ID {user.Id} not found");

            _users[user.Id] = user;

            _logger.LogInformation("Updated user: {Username} (ID: {UserId})", user.Username, user.Id);
            return Task.FromResult(user);
        }
    }

    public Task<List<User>> GetAllAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_users.Values.ToList());
        }
    }

    public async Task<User?> ValidateCredentialsAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await FindByUsernameAsync(username);

        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Authentication failed: user not found or inactive - {Username}", username);
            return null;
        }

        // Check if account is locked out
        if (user.IsLockedOut())
        {
            var lockoutRemaining = user.LockoutEnd!.Value - DateTime.UtcNow;
            _logger.LogWarning("Authentication failed: account locked out for {Username}. Lockout expires in {Minutes} minutes",
                username, Math.Ceiling(lockoutRemaining.TotalMinutes));
            return null;
        }

        // Clear expired lockout
        if (user.LockoutEnd != null && user.LockoutEnd.Value <= DateTime.UtcNow)
        {
            user.LockoutEnd = null;
            user.FailedLoginAttempts = 0;
            _logger.LogInformation("Account lockout expired and cleared for user: {Username}", username);
        }

        // Verify password using BCrypt
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            // Increment failed login attempts
            user.FailedLoginAttempts++;
            _logger.LogWarning("Authentication failed: invalid password - {Username}. Failed attempts: {FailedAttempts}",
                username, user.FailedLoginAttempts);

            // Lock account after 5 failed attempts
            const int maxFailedAttempts = 5;
            if (user.FailedLoginAttempts >= maxFailedAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                _logger.LogWarning("Account locked out for user: {Username}. Too many failed login attempts ({Attempts}). Lockout expires at {LockoutEnd}",
                    username, user.FailedLoginAttempts, user.LockoutEnd);
            }

            return null;
        }

        // Successful login - reset failed attempts and update last login time
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;

        _logger.LogInformation("User authenticated successfully: {Username} (ID: {UserId})",
            user.Username, user.Id);

        return user;
    }
}
