using System.Net.Http.Json;
using System.Text.Json;
using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.IntegrationTests.Helpers;

/// <summary>
/// Helper class for authentication operations in integration tests.
/// Provides methods to obtain JWT tokens for different user roles.
/// </summary>
public class AuthHelper
{
    private readonly HttpClient _client;
    private readonly Dictionary<string, string> _tokenCache = new();

    public AuthHelper(HttpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Gets a JWT token for the admin user (full access).
    /// </summary>
    public async Task<string> GetAdminTokenAsync()
    {
        return await GetTokenAsync("admin", "Admin123!");
    }

    /// <summary>
    /// Gets a JWT token for the deployer user (can create deployments).
    /// </summary>
    public async Task<string> GetDeployerTokenAsync()
    {
        return await GetTokenAsync("deployer", "Deploy123!");
    }

    /// <summary>
    /// Gets a JWT token for the viewer user (read-only access).
    /// </summary>
    public async Task<string> GetViewerTokenAsync()
    {
        return await GetTokenAsync("viewer", "Viewer123!");
    }

    /// <summary>
    /// Gets a JWT token for the specified username and password.
    /// Caches tokens to avoid unnecessary authentication calls.
    /// </summary>
    public async Task<string> GetTokenAsync(string username, string password)
    {
        // Check cache first
        var cacheKey = $"{username}:{password}";
        if (_tokenCache.TryGetValue(cacheKey, out var cachedToken))
        {
            return cachedToken;
        }

        // Login and get token
        var loginRequest = new AuthenticationRequest
        {
            Username = username,
            Password = password
        };

        var response = await _client.PostAsJsonAsync("/api/v1/authentication/login", loginRequest);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<AuthenticationResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (loginResponse?.Token == null)
        {
            throw new InvalidOperationException($"Failed to get token for user '{username}'");
        }

        // Cache the token
        _tokenCache[cacheKey] = loginResponse.Token;

        return loginResponse.Token;
    }

    /// <summary>
    /// Adds the Authorization header with a Bearer token to the HttpClient.
    /// </summary>
    public void AddAuthorizationHeader(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Clears the token cache (useful for testing token expiration).
    /// </summary>
    public void ClearTokenCache()
    {
        _tokenCache.Clear();
    }
}
