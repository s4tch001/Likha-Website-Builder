using System.Text.Json.Serialization;

namespace WebsiteBuilder.Core.Models;

/// <summary>
/// A single node in the design tree. This is the heart of the Project JSON
/// model and the unit produced/consumed by the React editor and the code
/// generators. The shape intentionally matches the JSON contract documented
/// in the project brief, e.g.:
/// <code>
/// { "id": "button001", "type": "Button", "x": 150, "y": 300,
///   "width": 180, "height": 45, "text": "Login",
///   "styles": { "background": "#2563eb", "color": "#ffffff" } }
/// </code>
/// </summary>
public sealed class ElementNode
{
    /// <summary>Unique, stable id within a page (used by selection, layers, codegen).</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Element kind as a canonical string (see <see cref="ElementTypes"/>).</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = ElementTypes.Div;

    /// <summary>Optional author-facing name shown in the Layers panel.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    // --- Geometry (base breakpoint) ---
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("width")] public double Width { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }

    /// <summary>Inline text content for text-bearing elements (Button, Heading, etc.).</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Rotation in degrees, applied around the element's center.</summary>
    [JsonPropertyName("rotation")]
    public double Rotation { get; set; }

    /// <summary>
    /// Arbitrary HTML attributes (href, src, placeholder, type, ...) that are
    /// not styles. Kept open-ended so new element kinds need no schema change.
    /// </summary>
    [JsonPropertyName("attributes")]
    public Dictionary<string, string> Attributes { get; set; } = new();

    /// <summary>
    /// Base-breakpoint CSS declarations as property/value pairs
    /// (e.g. "background" -> "#2563eb"). Generators emit these as reusable classes.
    /// </summary>
    [JsonPropertyName("styles")]
    public Dictionary<string, string> Styles { get; set; } = new();

    /// <summary>
    /// Per-breakpoint style overrides keyed by <see cref="Breakpoint.Id"/>.
    /// Only properties that differ from the base are stored here.
    /// </summary>
    [JsonPropertyName("responsiveStyles")]
    public Dictionary<string, Dictionary<string, string>> ResponsiveStyles { get; set; } = new();

    /// <summary>Editor-only flags surfaced in the Layers panel.</summary>
    [JsonPropertyName("hidden")] public bool Hidden { get; set; }
    [JsonPropertyName("locked")] public bool Locked { get; set; }

    /// <summary>Child nodes, enabling the full nested layout tree.</summary>
    [JsonPropertyName("children")]
    public List<ElementNode> Children { get; set; } = new();

    /// <summary>Depth-first enumeration of this node and all descendants.</summary>
    public IEnumerable<ElementNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var node in child.DescendantsAndSelf())
            {
                yield return node;
            }
        }
    }
}
