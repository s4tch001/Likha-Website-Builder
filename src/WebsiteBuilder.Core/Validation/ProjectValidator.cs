using System.Diagnostics.CodeAnalysis;
using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.Core.Validation;

/// <summary>Validates the complete canonical project model at every trust boundary.</summary>
public static class ProjectValidator
{
    public const int MaxJsonCharacters = 16 * 1024 * 1024;
    private const int MaxPages = 1_000;
    private const int MaxElements = 100_000;
    private const int MaxTreeDepth = 128;

    /// <summary>Throws a path-specific exception when the project is not safe and well formed.</summary>
    public static void ValidateAndThrow(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        RequireText(project.Id, 128, "project.id");
        RequireText(project.Name, 512, "project.name");
        if (project.SchemaVersion != Project.CurrentSchemaVersion)
        {
            Fail($"project.schemaVersion must be {Project.CurrentSchemaVersion}.");
        }

        if (project.Pages is null || project.Pages.Count is < 1 or > MaxPages)
        {
            Fail($"project.pages must contain between 1 and {MaxPages} pages.");
        }

        if (project.Breakpoints is null || project.Breakpoints.Count is < 1 or > 64)
        {
            Fail("project.breakpoints must contain between 1 and 64 entries.");
        }

        if (project.Variables is null || project.Variables.Count > 1_000)
        {
            Fail("project.variables is invalid or too large.");
        }

        if (project.Assets is null || project.Assets.Count > 10_000)
        {
            Fail("project.assets is invalid or too large.");
        }

        ValidateBreakpoints(project.Breakpoints);
        ValidateVariables(project.Variables);
        ValidateAssets(project.Assets);
        ValidatePages(project.Pages);
    }

    private static void ValidateBreakpoints(IReadOnlyList<Breakpoint> breakpoints)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var baseCount = 0;
        foreach (var breakpoint in breakpoints)
        {
            if (breakpoint is null)
            {
                Fail("project.breakpoints cannot contain null entries.");
            }

            RequireText(breakpoint.Id, 128, "breakpoint.id");
            RequireText(breakpoint.Label, 256, $"breakpoint[{breakpoint.Id}].label");
            if (!ids.Add(breakpoint.Id))
            {
                Fail($"Duplicate breakpoint id '{breakpoint.Id}'.");
            }

            if (breakpoint.MaxWidth is < 0 or > 100_000)
            {
                Fail($"Breakpoint '{breakpoint.Id}' has an invalid maxWidth.");
            }

            if (breakpoint.IsBase)
            {
                baseCount++;
            }
        }

        if (baseCount != 1)
        {
            Fail("Exactly one breakpoint must be the base breakpoint.");
        }
    }

    private static void ValidateVariables(IReadOnlyDictionary<string, string> variables)
    {
        foreach (var (name, value) in variables)
        {
            var canonicalName = name.StartsWith("--", StringComparison.Ordinal) ? name : "--" + name;
            if (!ProjectContentPolicy.IsSafeCssPropertyName(canonicalName)
                || !ProjectContentPolicy.IsSafeCssValue(value ?? string.Empty))
            {
                Fail($"Project variable '{name}' is not a safe CSS declaration.");
            }
        }
    }

    private static void ValidateAssets(IReadOnlyList<ProjectAsset> assets)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var storedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            if (asset is null)
            {
                Fail("project.assets cannot contain null entries.");
            }

            RequireText(asset.Id, 128, "asset.id");
            RequireText(asset.Name, 512, $"asset[{asset.Id}].name");
            RequireText(asset.StoredFileName, 512, $"asset[{asset.Id}].storedFileName");
            RequireText(asset.RelativePath, 1_024, $"asset[{asset.Id}].relativePath");
            RequireText(asset.MediaType, 256, $"asset[{asset.Id}].mediaType");
            RequireText(asset.Sha256, 64, $"asset[{asset.Id}].sha256");
            if (!ids.Add(asset.Id) || !storedNames.Add(asset.StoredFileName))
            {
                Fail($"Duplicate asset identity or stored filename for '{asset.Id}'.");
            }

            if (Path.GetFileName(asset.StoredFileName) != asset.StoredFileName
                || asset.RelativePath != $"Assets/{asset.StoredFileName}"
                || asset.SizeBytes < 0
                || asset.Sha256.Length != 64
                || !asset.Sha256.All(Uri.IsHexDigit))
            {
                Fail($"Asset '{asset.Id}' has invalid storage metadata.");
            }
        }
    }

    private static void ValidatePages(IReadOnlyList<Page> pages)
    {
        var pageIds = new HashSet<string>(StringComparer.Ordinal);
        var routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var elementIds = new HashSet<string>(StringComparer.Ordinal);
        var elementCount = 0;

        foreach (var page in pages)
        {
            if (page is null || page.Root is null)
            {
                Fail("project.pages cannot contain null pages or roots.");
            }

            RequireText(page.Id, 128, "page.id");
            RequireText(page.Name, 512, $"page[{page.Id}].name");
            if (!pageIds.Add(page.Id))
            {
                Fail($"Duplicate page id '{page.Id}'.");
            }

            RequireText(page.Route, 200, $"page[{page.Id}].route");
            var route = page.Route.Trim('/');
            if (!ProjectContentPolicy.IsSafeRoute(route) || !routes.Add(route))
            {
                Fail($"Page '{page.Id}' has an unsafe or duplicate route '{page.Route}'.");
            }

            var stack = new Stack<(ElementNode Node, int Depth)>();
            stack.Push((page.Root, 0));
            while (stack.Count > 0)
            {
                var (node, depth) = stack.Pop();
                if (node is null || depth > MaxTreeDepth || ++elementCount > MaxElements)
                {
                    Fail("The element tree is null, too deep, or too large.");
                }

                ValidateNode(node, page.Id, elementIds);
                for (var index = node.Children.Count - 1; index >= 0; index--)
                {
                    stack.Push((node.Children[index], depth + 1));
                }
            }
        }
    }

    private static void ValidateNode(ElementNode node, string pageId, HashSet<string> ids)
    {
        RequireText(node.Id, 128, $"page[{pageId}].element.id");
        RequireText(node.Type, 128, $"element[{node.Id}].type");
        if (!ids.Add(node.Id))
        {
            Fail($"Duplicate element id '{node.Id}'. Element ids must be project-wide unique.");
        }

        if (!double.IsFinite(node.X)
            || !double.IsFinite(node.Y)
            || !double.IsFinite(node.Width)
            || !double.IsFinite(node.Height)
            || !double.IsFinite(node.Rotation)
            || node.Width < 0
            || node.Height < 0)
        {
            Fail($"Element '{node.Id}' has invalid geometry.");
        }

        if (node.Children is null || node.Attributes is null || node.Styles is null || node.ResponsiveStyles is null)
        {
            Fail($"Element '{node.Id}' has null collections.");
        }

        if (node.Text?.Length > 1_000_000 || node.Children.Count > 10_000)
        {
            Fail($"Element '{node.Id}' exceeds content or child limits.");
        }

        foreach (var (name, value) in node.Attributes)
        {
            if (!ProjectContentPolicy.IsSafeHtmlAttribute(name, value ?? string.Empty))
            {
                Fail($"Element '{node.Id}' contains unsafe attribute '{name}'.");
            }
        }

        ValidateStyles(node.Id, node.Styles);
        foreach (var (breakpointId, styles) in node.ResponsiveStyles)
        {
            RequireText(breakpointId, 128, $"element[{node.Id}].responsiveStyles key");
            if (styles is null)
            {
                Fail($"Element '{node.Id}' has a null responsive style layer.");
            }

            ValidateStyles(node.Id, styles);
        }
    }

    private static void ValidateStyles(string elementId, IReadOnlyDictionary<string, string> styles)
    {
        if (styles.Count > 2_000)
        {
            Fail($"Element '{elementId}' has too many style declarations.");
        }

        foreach (var (name, value) in styles)
        {
            if (!ProjectContentPolicy.IsSafeCssPropertyName(name)
                || !ProjectContentPolicy.IsSafeCssValue(value ?? string.Empty))
            {
                Fail($"Element '{elementId}' contains unsafe CSS property '{name}'.");
            }
        }
    }

    private static void RequireText(string? value, int maxLength, string path)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength || value.IndexOf('\0') >= 0)
        {
            Fail($"{path} is missing or invalid.");
        }
    }

    [DoesNotReturn]
    private static void Fail(string message) => throw new ProjectValidationException(message);
}
