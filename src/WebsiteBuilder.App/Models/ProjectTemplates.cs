using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.App.Models;

/// <summary>
/// Seeds a freshly created project with a small starter layout so the canvas has
/// something to render before the drag-and-drop engine (Phase 5) exists. This is
/// real model content — the renderer draws exactly these nodes.
/// </summary>
public static class ProjectTemplates
{
    /// <summary>Adds a hero heading, paragraph, button and card to the project's first page.</summary>
    public static void ApplyStarter(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var page = project.Pages.FirstOrDefault();
        if (page is null)
        {
            return;
        }

        var root = page.Root;
        root.Styles["background"] = "#0f0f12";
        root.Children.Clear();

        root.Children.Add(new ElementNode
        {
            Id = "hero-heading",
            Type = ElementTypes.Heading,
            Name = "Hero Heading",
            X = 80,
            Y = 80,
            Width = 720,
            Height = 64,
            Text = "Build websites visually",
            Styles =
            {
                ["color"] = "#f5f5fa",
                ["font-size"] = "44px",
                ["font-weight"] = "700",
                ["line-height"] = "1.1",
            },
        });

        root.Children.Add(new ElementNode
        {
            Id = "hero-subtitle",
            Type = ElementTypes.Paragraph,
            Name = "Hero Subtitle",
            X = 80,
            Y = 160,
            Width = 640,
            Height = 72,
            Text = "Design responsive pages with drag-and-drop and export clean HTML, CSS, JavaScript and React.",
            Styles =
            {
                ["color"] = "#9a9aa5",
                ["font-size"] = "18px",
                ["line-height"] = "1.5",
            },
        });

        root.Children.Add(new ElementNode
        {
            Id = "hero-button",
            Type = ElementTypes.Button,
            Name = "Get Started Button",
            X = 80,
            Y = 256,
            Width = 168,
            Height = 48,
            Text = "Get Started",
            Styles =
            {
                ["background"] = "#2563eb",
                ["color"] = "#ffffff",
                ["font-size"] = "16px",
                ["font-weight"] = "600",
                ["border-radius"] = "8px",
                ["display"] = "flex",
                ["align-items"] = "center",
                ["justify-content"] = "center",
            },
        });

        root.Children.Add(new ElementNode
        {
            Id = "feature-card",
            Type = ElementTypes.Card,
            Name = "Feature Card",
            X = 80,
            Y = 344,
            Width = 720,
            Height = 180,
            Styles =
            {
                ["background"] = "#1a1a20",
                ["border"] = "1px solid #2c2c34",
                ["border-radius"] = "14px",
            },
            Children =
            {
                new ElementNode
                {
                    Id = "card-title",
                    Type = ElementTypes.Heading,
                    Name = "Card Title",
                    X = 28, Y = 24, Width = 400, Height = 32,
                    Text = "Responsive by design",
                    Styles = { ["color"] = "#e6e6eb", ["font-size"] = "22px", ["font-weight"] = "600" },
                },
                new ElementNode
                {
                    Id = "card-body",
                    Type = ElementTypes.Paragraph,
                    Name = "Card Body",
                    X = 28, Y = 68, Width = 640, Height = 80,
                    Text = "Every element carries its own properties per breakpoint, so desktop, tablet and mobile each look right.",
                    Styles = { ["color"] = "#9a9aa5", ["font-size"] = "15px", ["line-height"] = "1.5" },
                },
            },
        });
    }
}
