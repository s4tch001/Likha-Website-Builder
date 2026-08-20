using System.Text.RegularExpressions;
using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Validation;

namespace WebsiteBuilder.Core.Components;

/// <summary>A first-party reusable element tree exposed by the component library.</summary>
public sealed record ComponentDefinition(
    string Id,
    string Name,
    string Category,
    string Description,
    string Glyph,
    IReadOnlyList<string> Tags,
    ElementNode Root);

/// <summary>Validates compiled component definitions before they cross into the editor.</summary>
public static partial class ComponentDefinitionValidator
{
    public static void ValidateAndThrow(ComponentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!IdRegex().IsMatch(definition.Id)
            || string.IsNullOrWhiteSpace(definition.Name) || definition.Name.Length > 160
            || string.IsNullOrWhiteSpace(definition.Category) || definition.Category.Length > 80
            || string.IsNullOrWhiteSpace(definition.Description) || definition.Description.Length > 500
            || string.IsNullOrWhiteSpace(definition.Glyph) || definition.Glyph.Length > 8
            || definition.Tags.Count > 32
            || definition.Tags.Any(tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 64))
        {
            throw new ProjectValidationException($"Component definition '{definition.Id}' has invalid metadata.");
        }

        var project = Project.CreateDefault("Component validation");
        project.Pages[0].Root.Children.Add(definition.Root);
        ProjectValidator.ValidateAndThrow(project);
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdRegex();
}
