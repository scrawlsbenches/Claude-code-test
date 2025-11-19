using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a user in the system with authentication and authorization details.
/// </summary>
public class User
{
    /// <summary>
    /// Unique identifier for the user.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Username for authentication (must be unique).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// User's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// BCrypt hashed password.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Full name of the user.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// User's assigned roles for access control.
    /// </summary>
    public List<UserRole> Roles { get; set; } = new();

    /// <summary>
    /// Indicates whether the user account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date and time when the user account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time of the user's last login.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Number of consecutive failed login attempts.
    /// Reset to 0 after successful login.
    /// </summary>
    public int FailedLoginAttempts { get; set; } = 0;

    /// <summary>
    /// Date and time when the account lockout expires.
    /// Null if account is not locked out.
    /// </summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>
    /// Checks if the user account is currently locked out.
    /// </summary>
    public bool IsLockedOut()
    {
        if (LockoutEnd == null)
            return false;

        // If lockout period has expired, account is no longer locked
        if (LockoutEnd.Value <= DateTime.UtcNow)
        {
            // Auto-unlock expired lockouts
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if the user has a specific role.
    /// </summary>
    public bool HasRole(UserRole role) => Roles.Contains(role);

    /// <summary>
    /// Checks if the user has admin privileges.
    /// </summary>
    public bool IsAdmin() => Roles.Contains(UserRole.Admin);
}
