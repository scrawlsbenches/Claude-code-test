using HotSwap.Distributed.Domain.Enums;

namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a website plugin (functionality extension).
/// </summary>
public class Plugin
{
    /// <summary>
    /// Unique identifier for the plugin.
    /// </summary>
    public Guid PluginId { get; set; }

    /// <summary>
    /// Plugin name (e.g., "Contact Form Builder").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Plugin version (semver).
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Plugin category.
    /// </summary>
    public PluginCategory Category { get; set; }

    /// <summary>
    /// Plugin dependencies (other plugin names/versions required).
    /// </summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Plugin package (zip file containing code, assets, etc.).
    /// </summary>
    public byte[]? PluginPackage { get; set; }

    /// <summary>
    /// Plugin description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Plugin author/developer.
    /// </summary>
    public required string Author { get; set; }

    /// <summary>
    /// Whether the plugin is available in the public marketplace.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Plugin manifest with metadata and configuration.
    /// </summary>
    public PluginManifest Manifest { get; set; } = new();

    /// <summary>
    /// Date and time when the plugin was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when the plugin was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Plugin manifest containing metadata and configuration.
/// </summary>
public class PluginManifest
{
    /// <summary>
    /// Plugin name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Plugin version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Required permissions (e.g., "database.write", "api.call").
    /// </summary>
    public List<string> RequiredPermissions { get; set; } = new();

    /// <summary>
    /// Default plugin settings.
    /// </summary>
    public Dictionary<string, object> DefaultSettings { get; set; } = new();

    /// <summary>
    /// Hook registrations (events the plugin subscribes to).
    /// </summary>
    public List<HookRegistration> Hooks { get; set; } = new();

    /// <summary>
    /// Custom API endpoints provided by the plugin.
    /// </summary>
    public List<ApiEndpoint> ApiEndpoints { get; set; } = new();
}

/// <summary>
/// Plugin hook registration.
/// </summary>
public class HookRegistration
{
    /// <summary>
    /// Hook name (e.g., "page.before_render", "user.login").
    /// </summary>
    public required string HookName { get; set; }

    /// <summary>
    /// Handler function name.
    /// </summary>
    public required string Handler { get; set; }

    /// <summary>
    /// Hook priority (lower = executes first).
    /// </summary>
    public int Priority { get; set; } = 10;
}

/// <summary>
/// Plugin API endpoint definition.
/// </summary>
public class ApiEndpoint
{
    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE).
    /// </summary>
    public required string Method { get; set; }

    /// <summary>
    /// Endpoint path (e.g., "/api/contact-form/submit").
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Handler function name.
    /// </summary>
    public required string Handler { get; set; }

    /// <summary>
    /// Whether authentication is required.
    /// </summary>
    public bool RequiresAuth { get; set; }
}
