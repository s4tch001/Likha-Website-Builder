using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.Core.Components;

/// <summary>Version-controlled first-party templates; no runtime code or external files are loaded.</summary>
public static class BuiltInComponentLibrary
{
    public static IReadOnlyList<ComponentDefinition> All { get; } = BuildAndValidate();

    private static IReadOnlyList<ComponentDefinition> BuildAndValidate()
    {
        ComponentDefinition[] definitions = [BuildSimpleHero()];
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
}
