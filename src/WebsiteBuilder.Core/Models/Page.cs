using System.Text.Json.Serialization;

namespace WebsiteBuilder.Core.Models;

/// <summary>
/// One designable page/route within a project. A page owns a single root
/// element tree; generators turn each page into an HTML document or a React route.
/// </summary>
public sealed class Page
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Display name (e.g. "Home").</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Home";

    /// <summary>URL path / output file stem (e.g. "index", "about").</summary>
    [JsonPropertyName("route")]
    public string Route { get; set; } = "index";

    /// <summary>The root of this page's element tree.</summary>
    [JsonPropertyName("root")]
    public ElementNode Root { get; set; } = new()
    {
        Id = "root",
        Type = ElementTypes.Section,
        Name = "Page Root",
    };
}
