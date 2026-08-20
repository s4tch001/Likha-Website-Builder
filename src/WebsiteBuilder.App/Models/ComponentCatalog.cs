using WebsiteBuilder.Core.Components;
using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.App.Models;

/// <summary>A draggable entry in the Components toolbox.</summary>
/// <param name="ElementType">Canonical element type id (see <see cref="ElementTypes"/>).</param>
/// <param name="DisplayName">Label shown in the toolbox.</param>
/// <param name="Glyph">Short text/emoji glyph used as a lightweight icon.</param>
public sealed record ComponentItem(
    string ElementType,
    string DisplayName,
    string Glyph,
    string Description = "",
    ComponentDefinition? Definition = null)
{
    public bool IsBlock => Definition is not null;
    public string ToolTip => string.IsNullOrEmpty(Description) ? $"Click to add {DisplayName}" : Description;
    public string SearchText => Definition is null
        ? $"{DisplayName} {ElementType}"
        : $"{DisplayName} {Description} {string.Join(' ', Definition.Tags)}";
}

/// <summary>A named group of toolbox items (e.g. "Layout", "Typography").</summary>
public sealed record ComponentGroup(string Name, IReadOnlyList<ComponentItem> Items);

/// <summary>
/// The full palette of elements that can be dragged onto the canvas, grouped for
/// the Components panel. This is the authoritative toolbox list; the actual
/// drag-and-drop wiring into the editor is implemented in Phase 5, but the data
/// shown here is real and complete.
/// </summary>
public static class ComponentCatalog
{
    public static IReadOnlyList<ComponentGroup> Groups { get; } = new[]
    {
        new ComponentGroup("Blocks", BuiltInComponentLibrary.All
            .Select(definition => new ComponentItem(
                definition.Root.Type,
                definition.Name,
                definition.Glyph,
                definition.Description,
                definition))
            .ToArray()),
        new ComponentGroup("Layout", new[]
        {
            new ComponentItem(ElementTypes.Section, "Section", "▭"),
            new ComponentItem(ElementTypes.Container, "Container", "▢"),
            new ComponentItem(ElementTypes.Div, "Div", "◻"),
            new ComponentItem(ElementTypes.Navbar, "Navbar", "≡"),
            new ComponentItem(ElementTypes.Sidebar, "Sidebar", "▥"),
            new ComponentItem(ElementTypes.Footer, "Footer", "▁"),
            new ComponentItem(ElementTypes.Card, "Card", "🂠"),
        }),
        new ComponentGroup("Typography", new[]
        {
            new ComponentItem(ElementTypes.Heading, "Heading", "H"),
            new ComponentItem(ElementTypes.Paragraph, "Paragraph", "¶"),
            new ComponentItem(ElementTypes.Text, "Text", "T"),
            new ComponentItem(ElementTypes.Link, "Link", "🔗"),
        }),
        new ComponentGroup("Interactive", new[]
        {
            new ComponentItem(ElementTypes.Button, "Button", "⬚"),
        }),
        new ComponentGroup("Media", new[]
        {
            new ComponentItem(ElementTypes.Image, "Image", "🖼"),
            new ComponentItem(ElementTypes.Video, "Video", "▶"),
            new ComponentItem(ElementTypes.Icon, "Icon", "★"),
            new ComponentItem(ElementTypes.Svg, "SVG", "◇"),
            new ComponentItem(ElementTypes.Canvas, "Canvas", "▦"),
        }),
        new ComponentGroup("Forms", new[]
        {
            new ComponentItem(ElementTypes.Form, "Form", "▤"),
            new ComponentItem(ElementTypes.Input, "Input", "▭"),
            new ComponentItem(ElementTypes.Textarea, "Textarea", "▦"),
            new ComponentItem(ElementTypes.Checkbox, "Checkbox", "☑"),
            new ComponentItem(ElementTypes.Radio, "Radio", "◉"),
            new ComponentItem(ElementTypes.Dropdown, "Dropdown", "▾"),
        }),
        new ComponentGroup("Data", new[]
        {
            new ComponentItem(ElementTypes.Table, "Table", "▦"),
            new ComponentItem(ElementTypes.List, "List", "☰"),
        }),
        new ComponentGroup("Widgets", new[]
        {
            new ComponentItem(ElementTypes.Accordion, "Accordion", "▤"),
            new ComponentItem(ElementTypes.Tabs, "Tabs", "▭"),
            new ComponentItem(ElementTypes.Modal, "Modal", "❒"),
            new ComponentItem(ElementTypes.Alert, "Alert", "⚠"),
            new ComponentItem(ElementTypes.Badge, "Badge", "●"),
            new ComponentItem(ElementTypes.Avatar, "Avatar", "👤"),
            new ComponentItem(ElementTypes.ProgressBar, "Progress Bar", "▰"),
            new ComponentItem(ElementTypes.Spinner, "Spinner", "◌"),
            new ComponentItem(ElementTypes.Breadcrumb, "Breadcrumb", "›"),
            new ComponentItem(ElementTypes.Pagination, "Pagination", "⋯"),
        }),
        new ComponentGroup("Embeds", new[]
        {
            new ComponentItem(ElementTypes.GoogleMap, "Google Map", "📍"),
            new ComponentItem(ElementTypes.YouTube, "YouTube", "▶"),
            new ComponentItem(ElementTypes.CustomHtml, "Custom HTML", "</>"),
            new ComponentItem(ElementTypes.CustomJavaScript, "Custom JS", "{}"),
            new ComponentItem(ElementTypes.CustomCss, "Custom CSS", "#"),
        }),
    };
}
