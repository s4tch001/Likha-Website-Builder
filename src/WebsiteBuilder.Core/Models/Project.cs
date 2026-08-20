using System.Text.Json.Serialization;

namespace WebsiteBuilder.Core.Models;

/// <summary>
/// The top-level document persisted as <c>Project.json</c>. This is the single
/// source of truth for the whole application: the editor mutates it, and every
/// code generator reads from it. HTML/CSS/JS/React are always derived, never
/// authored directly.
/// </summary>
public sealed class Project
{
    /// <summary>Schema version, so the serializer can migrate older files forward.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public const int CurrentSchemaVersion = 2;

    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Untitled Project";

    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("modifiedUtc")]
    public DateTimeOffset ModifiedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Responsive breakpoints available in this project.</summary>
    [JsonPropertyName("breakpoints")]
    public List<Breakpoint> Breakpoints { get; set; } = new(Breakpoint.Defaults);

    /// <summary>The pages that make up the site.</summary>
    [JsonPropertyName("pages")]
    public List<Page> Pages { get; set; } = new();

    /// <summary>Project-wide CSS custom properties / design tokens (name -> value).</summary>
    [JsonPropertyName("variables")]
    public Dictionary<string, string> Variables { get; set; } = new();

    /// <summary>Files securely imported into the folder-based project's Assets directory.</summary>
    [JsonPropertyName("assets")]
    public List<ProjectAsset> Assets { get; set; } = new();

    /// <summary>Creates a new, empty project containing a single Home page.</summary>
    public static Project CreateDefault(string name = "Untitled Project")
    {
        return new Project
        {
            Name = name,
            Pages = { new Page { Name = "Home", Route = "index" } },
        };
    }
}
