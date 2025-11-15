using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Infrastructure.Interfaces;

/// <summary>
/// Repository for user management operations.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Finds a user by username.
    /// </summary>
    /// <param name="username">Username to search for</param>
    /// <returns>User if found, null otherwise</returns>
    Task<User?> FindByUsernameAsync(string username);

    /// <summary>
    /// Finds a user by ID.
    /// </summary>
    /// <param name="userId">User ID to search for</param>
    /// <returns>User if found, null otherwise</returns>
    Task<User?> FindByIdAsync(Guid userId);

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="user">User to create</param>
    /// <returns>Created user</returns>
    Task<User> CreateAsync(User user);

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    /// <param name="user">User to update</param>
    /// <returns>Updated user</returns>
    Task<User> UpdateAsync(User user);

    /// <summary>
    /// Gets all users.
    /// </summary>
    /// <returns>List of all users</returns>
    Task<List<User>> GetAllAsync();

    /// <summary>
    /// Verifies a user's password.
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="password">Password to verify</param>
    /// <returns>User if credentials are valid, null otherwise</returns>
    Task<User?> ValidateCredentialsAsync(string username, string password);
}
