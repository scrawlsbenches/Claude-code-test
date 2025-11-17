namespace HotSwap.Distributed.Domain.Models;

/// <summary>
/// Represents a website theme (visual appearance and layout).
/// </summary>
public class Theme
{
    /// <summary>
    /// Unique identifier for the theme.
    /// </summary>
    public Guid ThemeId { get; set; }

    /// <summary>
    /// Theme name (e.g., "Modern Blog").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Theme version (semver).
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Theme author/developer.
    /// </summary>
    public required string Author { get; set; }

    /// <summary>
    /// Whether the theme is available in the public marketplace.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Theme package (zip file containing templates, assets, etc.).
    /// </summary>
    public byte[]? ThemePackage { get; set; }

    /// <summary>
    /// Preview/screenshot image URL.
    /// </summary>
    public string? PreviewImageUrl { get; set; }

    /// <summary>
    /// Theme description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Theme manifest with metadata and configuration.
    /// </summary>
    public ThemeManifest Manifest { get; set; } = new();

    /// <summary>
    /// Date and time when the theme was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when the theme was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Theme manifest containing metadata and configuration.
/// </summary>
public class ThemeManifest
{
    /// <summary>
    /// Theme name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Theme version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// List of template files (e.g., "index.html", "post.html").
    /// </summary>
    public List<string> Templates { get; set; } = new();

    /// <summary>
    /// List of stylesheet files.
    /// </summary>
    public List<string> Stylesheets { get; set; } = new();

    /// <summary>
    /// List of JavaScript files.
    /// </summary>
    public List<string> Scripts { get; set; } = new();

    /// <summary>
    /// Customization options (colors, fonts, etc.).
    /// </summary>
    public Dictionary<string, ThemeCustomization> CustomizationOptions { get; set; } = new();
}

/// <summary>
/// Theme customization option.
/// </summary>
public class ThemeCustomization
{
    /// <summary>
    /// Customization type (color, font, size, etc.).
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Default value.
    /// </summary>
    public required string DefaultValue { get; set; }

    /// <summary>
    /// Current value (if customized).
    /// </summary>
    public string? CurrentValue { get; set; }

    /// <summary>
    /// Label for UI display.
    /// </summary>
    public required string Label { get; set; }
}
