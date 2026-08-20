using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.CodeGen;

/// <summary>
/// Shared element-type → HTML tag mapping used by both the static-HTML and React
/// generators (React DOM renders the same lowercase HTML tags).
/// </summary>
internal static class HtmlTags
{
    private static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ElementTypes.Section] = "section",
        [ElementTypes.Container] = "div",
        [ElementTypes.Div] = "div",
        [ElementTypes.Card] = "div",
        [ElementTypes.Navbar] = "nav",
        [ElementTypes.Footer] = "footer",
        [ElementTypes.Sidebar] = "aside",
        [ElementTypes.Heading] = "h2",
        [ElementTypes.Paragraph] = "p",
        [ElementTypes.Text] = "span",
        [ElementTypes.Button] = "button",
        [ElementTypes.Link] = "a",
        [ElementTypes.Image] = "img",
        [ElementTypes.Video] = "video",
        [ElementTypes.Form] = "form",
        [ElementTypes.Input] = "input",
        [ElementTypes.Textarea] = "textarea",
        [ElementTypes.List] = "ul",
        [ElementTypes.Table] = "table",
    };

    /// <summary>Void elements that never have children or a closing tag.</summary>
    private static readonly HashSet<string> Void = new(StringComparer.Ordinal) { "img", "input", "br", "hr" };

    public static string TagFor(string type) => Map.TryGetValue(type, out var tag) ? tag : "div";

    public static bool IsVoid(string tag) => Void.Contains(tag);
}
