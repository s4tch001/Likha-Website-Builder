using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.Core.Components;

/// <summary>Version-controlled first-party templates; no runtime code or external files are loaded.</summary>
public static class BuiltInComponentLibrary
{
    public static IReadOnlyList<ComponentDefinition> All { get; } = BuildAndValidate();

    private static IReadOnlyList<ComponentDefinition> BuildAndValidate()
    {
        ComponentDefinition[] definitions =
        [
            BuildNavbar(),
            BuildSimpleHero(),
            BuildSplitHero(),
            BuildFooter(),
            BuildNotFound(),
        ];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            ComponentDefinitionValidator.ValidateAndThrow(definition);
            if (!ids.Add(definition.Id))
            {
                throw new InvalidOperationException($"Duplicate component definition id '{definition.Id}'.");
            }
        }

        return Array.AsReadOnly(definitions);
    }

    private static ComponentDefinition BuildSimpleHero() => new(
        "hero-simple",
        "Simple Hero",
        "Hero",
        "Headline, supporting copy, and primary call to action.",
        "H",
        ["hero", "header", "call to action", "landing"],
        new ElementNode
        {
            Id = "tpl-hero-simple",
            Type = ElementTypes.Section,
            Name = "Simple Hero",
            Width = 1000,
            Height = 420,
            Styles =
            {
                ["background"] = "linear-gradient(135deg, #111827, #1e3a8a)",
                ["border-radius"] = "20px",
                ["overflow"] = "hidden",
            },
            ResponsiveStyles =
            {
                ["mobile"] = new() { ["border-radius"] = "12px" },
            },
            Children =
            {
                new ElementNode
                {
                    Id = "tpl-hero-simple-kicker",
                    Type = ElementTypes.Text,
                    Name = "Eyebrow",
                    X = 64, Y = 64, Width = 360, Height = 24,
                    Text = "DESIGNED IN LIKHA",
                    Styles = { ["color"] = "#93c5fd", ["font-size"] = "13px", ["font-weight"] = "700", ["letter-spacing"] = "1.5px" },
                },
                new ElementNode
                {
                    Id = "tpl-hero-simple-title",
                    Type = ElementTypes.Heading,
                    Name = "Hero Title",
                    X = 64, Y = 104, Width = 760, Height = 112,
                    Text = "Turn an idea into a polished website",
                    Styles = { ["color"] = "#ffffff", ["font-size"] = "48px", ["font-weight"] = "750", ["line-height"] = "1.08" },
                },
                new ElementNode
                {
                    Id = "tpl-hero-simple-copy",
                    Type = ElementTypes.Paragraph,
                    Name = "Hero Copy",
                    X = 64, Y = 232, Width = 650, Height = 62,
                    Text = "Compose responsive sections visually, then export production-ready HTML or Next.js.",
                    Styles = { ["color"] = "#dbeafe", ["font-size"] = "18px", ["line-height"] = "1.55" },
                },
                new ElementNode
                {
                    Id = "tpl-hero-simple-action",
                    Type = ElementTypes.Button,
                    Name = "Primary Action",
                    X = 64, Y = 320, Width = 176, Height = 48,
                    Text = "Start building",
                    Styles =
                    {
                        ["background"] = "#ffffff", ["color"] = "#1e3a8a",
                        ["border-radius"] = "10px", ["font-size"] = "15px", ["font-weight"] = "700",
                        ["display"] = "flex", ["align-items"] = "center", ["justify-content"] = "center",
                    },
                },
            },
        });

    private static ComponentDefinition BuildNavbar() => new(
        "navbar-centered",
        "Centered Navbar",
        "Navigation",
        "Brand, primary navigation links, and a clear call to action.",
        "≡",
        ["navbar", "navigation", "header", "menu"],
        new ElementNode
        {
            Id = "tpl-navbar",
            Type = ElementTypes.Navbar,
            Name = "Centered Navbar",
            Width = 1100,
            Height = 76,
            Styles =
            {
                ["background"] = "#ffffff", ["border"] = "1px solid #e5e7eb",
                ["border-radius"] = "14px", ["box-shadow"] = "0 10px 30px rgba(15, 23, 42, 0.08)",
            },
            ResponsiveStyles = { ["mobile"] = new() { ["height"] = "132px" } },
            Children =
            {
                TextNode("tpl-navbar-brand", ElementTypes.Heading, "Brand", 28, 22, 190, 32, "Northstar",
                    ("color", "#0f172a"), ("font-size", "23px"), ("font-weight", "800")),
                LinkNode("tpl-navbar-work", "Work Link", 390, 27, 70, "Work", "#work"),
                LinkNode("tpl-navbar-about", "About Link", 480, 27, 72, "About", "#about"),
                LinkNode("tpl-navbar-contact", "Contact Link", 572, 27, 82, "Contact", "#contact"),
                new ElementNode
                {
                    Id = "tpl-navbar-action", Type = ElementTypes.Link, Name = "Navigation Action",
                    X = 916, Y = 16, Width = 156, Height = 44, Text = "Start a project",
                    Attributes = { ["href"] = "#contact" },
                    Styles =
                    {
                        ["background"] = "#0f172a", ["color"] = "#ffffff", ["border-radius"] = "9px",
                        ["font-size"] = "14px", ["font-weight"] = "700", ["text-decoration"] = "none",
                        ["display"] = "flex", ["align-items"] = "center", ["justify-content"] = "center",
                    },
                    ResponsiveStyles = { ["mobile"] = new() { ["left"] = "28px", ["top"] = "72px" } },
                },
            },
        });

    private static ComponentDefinition BuildSplitHero() => new(
        "hero-split",
        "Split Hero",
        "Hero",
        "Editorial hero with conversion copy and a visual product card.",
        "◧",
        ["hero", "split", "saas", "product", "landing"],
        new ElementNode
        {
            Id = "tpl-split-hero",
            Type = ElementTypes.Section,
            Name = "Split Hero",
            Width = 1100,
            Height = 540,
            Styles = { ["background"] = "#f8fafc", ["border-radius"] = "24px", ["overflow"] = "hidden" },
            ResponsiveStyles = { ["mobile"] = new() { ["height"] = "760px" } },
            Children =
            {
                TextNode("tpl-split-kicker", ElementTypes.Text, "Eyebrow", 64, 72, 430, 24, "A CALMER WAY TO SHIP",
                    ("color", "#2563eb"), ("font-size", "13px"), ("font-weight", "750"), ("letter-spacing", "1.4px")),
                new ElementNode
                {
                    Id = "tpl-split-title", Type = ElementTypes.Heading, Name = "Hero Title",
                    X = 64, Y = 112, Width = 500, Height = 154, Text = "Build momentum, not busywork.",
                    Styles = { ["color"] = "#0f172a", ["font-size"] = "54px", ["font-weight"] = "780", ["line-height"] = "1.02" },
                    ResponsiveStyles = { ["mobile"] = new() { ["width"] = "420px", ["font-size"] = "42px" } },
                },
                TextNode("tpl-split-copy", ElementTypes.Paragraph, "Hero Copy", 64, 286, 470, 84,
                    "A focused workspace for teams that want fewer handoffs and more meaningful launches.",
                    ("color", "#475569"), ("font-size", "18px"), ("line-height", "1.55")),
                new ElementNode
                {
                    Id = "tpl-split-action", Type = ElementTypes.Link, Name = "Primary Action",
                    X = 64, Y = 400, Width = 180, Height = 50, Text = "Explore the product",
                    Attributes = { ["href"] = "#product" },
                    Styles =
                    {
                        ["background"] = "#2563eb", ["color"] = "#ffffff", ["border-radius"] = "10px",
                        ["font-size"] = "15px", ["font-weight"] = "700", ["text-decoration"] = "none",
                        ["display"] = "flex", ["align-items"] = "center", ["justify-content"] = "center",
                    },
                },
                new ElementNode
                {
                    Id = "tpl-split-visual", Type = ElementTypes.Card, Name = "Product Preview",
                    X = 620, Y = 58, Width = 416, Height = 424,
                    Styles =
                    {
                        ["background"] = "linear-gradient(160deg, #1e293b, #0f172a)",
                        ["border"] = "1px solid #334155", ["border-radius"] = "22px",
                        ["box-shadow"] = "0 28px 60px rgba(15, 23, 42, 0.22)",
                    },
                    ResponsiveStyles = { ["mobile"] = new() { ["left"] = "64px", ["top"] = "500px", ["width"] = "420px", ["height"] = "210px" } },
                    Children =
                    {
                        TextNode("tpl-split-visual-label", ElementTypes.Text, "Preview Label", 32, 30, 220, 22, "Launch readiness",
                            ("color", "#cbd5e1"), ("font-size", "14px"), ("font-weight", "650")),
                        TextNode("tpl-split-visual-score", ElementTypes.Heading, "Preview Score", 32, 84, 260, 74, "94%",
                            ("color", "#ffffff"), ("font-size", "64px"), ("font-weight", "780")),
                        TextNode("tpl-split-visual-note", ElementTypes.Paragraph, "Preview Note", 32, 180, 330, 62,
                            "Everything important is aligned for this release.",
                            ("color", "#94a3b8"), ("font-size", "16px"), ("line-height", "1.5")),
                    },
                },
            },
        });

    private static ComponentDefinition BuildFooter() => new(
        "footer-multicolumn",
        "Multi-column Footer",
        "Footer",
        "Brand summary, grouped links, and copyright line.",
        "▁",
        ["footer", "navigation", "links", "legal"],
        new ElementNode
        {
            Id = "tpl-footer",
            Type = ElementTypes.Footer,
            Name = "Multi-column Footer",
            Width = 1100,
            Height = 300,
            Styles = { ["background"] = "#0f172a", ["border-radius"] = "20px" },
            ResponsiveStyles = { ["mobile"] = new() { ["height"] = "470px" } },
            Children =
            {
                TextNode("tpl-footer-brand", ElementTypes.Heading, "Footer Brand", 52, 50, 240, 36, "Northstar",
                    ("color", "#ffffff"), ("font-size", "26px"), ("font-weight", "800")),
                TextNode("tpl-footer-summary", ElementTypes.Paragraph, "Brand Summary", 52, 104, 330, 70,
                    "Thoughtful digital products for ambitious teams and growing businesses.",
                    ("color", "#94a3b8"), ("font-size", "15px"), ("line-height", "1.55")),
                TextNode("tpl-footer-product", ElementTypes.Text, "Product Heading", 520, 54, 120, 22, "PRODUCT",
                    ("color", "#64748b"), ("font-size", "12px"), ("font-weight", "750"), ("letter-spacing", "1px")),
                LinkNode("tpl-footer-features", "Features Link", 520, 92, 120, "Features", "#features", dark: true),
                LinkNode("tpl-footer-pricing", "Pricing Link", 520, 128, 120, "Pricing", "#pricing", dark: true),
                TextNode("tpl-footer-company", ElementTypes.Text, "Company Heading", 750, 54, 120, 22, "COMPANY",
                    ("color", "#64748b"), ("font-size", "12px"), ("font-weight", "750"), ("letter-spacing", "1px")),
                LinkNode("tpl-footer-about", "About Link", 750, 92, 120, "About", "#about", dark: true),
                LinkNode("tpl-footer-contact", "Contact Link", 750, 128, 120, "Contact", "#contact", dark: true),
                TextNode("tpl-footer-copy", ElementTypes.Text, "Copyright", 52, 244, 500, 22,
                    "© 2026 Northstar. All rights reserved.", ("color", "#64748b"), ("font-size", "13px")),
            },
        });

    private static ComponentDefinition BuildNotFound() => new(
        "page-404-centered",
        "Centered 404",
        "Utility",
        "Friendly not-found state with a route back home.",
        "404",
        ["404", "not found", "error", "utility"],
        new ElementNode
        {
            Id = "tpl-404",
            Type = ElementTypes.Section,
            Name = "Centered 404",
            Width = 1000,
            Height = 560,
            Styles = { ["background"] = "#f8fafc", ["border"] = "1px solid #e2e8f0", ["border-radius"] = "24px" },
            Children =
            {
                TextNode("tpl-404-code", ElementTypes.Heading, "Error Code", 300, 86, 400, 120, "404",
                    ("color", "#2563eb"), ("font-size", "108px"), ("font-weight", "850"), ("text-align", "center")),
                TextNode("tpl-404-title", ElementTypes.Heading, "Error Title", 260, 224, 480, 52, "This page wandered off",
                    ("color", "#0f172a"), ("font-size", "36px"), ("font-weight", "760"), ("text-align", "center")),
                TextNode("tpl-404-copy", ElementTypes.Paragraph, "Error Copy", 280, 294, 440, 58,
                    "The link may be outdated, or the page may have moved somewhere new.",
                    ("color", "#64748b"), ("font-size", "16px"), ("line-height", "1.55"), ("text-align", "center")),
                new ElementNode
                {
                    Id = "tpl-404-home", Type = ElementTypes.Link, Name = "Back Home",
                    X = 405, Y = 386, Width = 190, Height = 50, Text = "Back to homepage",
                    Attributes = { ["href"] = "index.html" },
                    Styles =
                    {
                        ["background"] = "#0f172a", ["color"] = "#ffffff", ["border-radius"] = "10px",
                        ["font-size"] = "15px", ["font-weight"] = "700", ["text-decoration"] = "none",
                        ["display"] = "flex", ["align-items"] = "center", ["justify-content"] = "center",
                    },
                },
            },
        });

    private static ElementNode TextNode(
        string id,
        string type,
        string name,
        double x,
        double y,
        double width,
        double height,
        string text,
        params (string Name, string Value)[] styles) => new()
        {
            Id = id,
            Type = type,
            Name = name,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Text = text,
            Styles = styles.ToDictionary(style => style.Name, style => style.Value, StringComparer.Ordinal),
        };

    private static ElementNode LinkNode(
        string id,
        string name,
        double x,
        double y,
        double width,
        string text,
        string href,
        bool dark = false) => new()
        {
            Id = id,
            Type = ElementTypes.Link,
            Name = name,
            X = x,
            Y = y,
            Width = width,
            Height = 24,
            Text = text,
            Attributes = { ["href"] = href },
            Styles =
            {
                ["color"] = dark ? "#cbd5e1" : "#475569",
                ["font-size"] = "14px", ["font-weight"] = "600", ["text-decoration"] = "none",
            },
        };
}
