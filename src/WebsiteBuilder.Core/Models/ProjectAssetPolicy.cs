using System.Globalization;

namespace WebsiteBuilder.Core.Models;

/// <summary>Shared, deterministic behavior for managed asset references.</summary>
public static class ProjectAssetPolicy
{
    /// <summary>A CSS-safe family name shared by the editor UI and exporters.</summary>
    public static string FontFamily(ProjectAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var id = new string(asset.Id.Where(char.IsAsciiLetterOrDigit).Take(16).ToArray());
        return "LikhaAsset_" + (id.Length == 0 ? "Font" : id);
    }

    /// <summary>Counts element attributes/styles that currently refer to an asset.</summary>
    public static int CountReferences(Project project, ProjectAsset asset)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(asset);

        var count = 0;
        var family = FontFamily(asset);
        foreach (var page in project.Pages)
        {
            var stack = new Stack<ElementNode>();
            stack.Push(page.Root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                count += node.Attributes.Values.Count(value =>
                    string.Equals(value, asset.RelativePath, StringComparison.Ordinal));
                count += node.Styles.Values.Count(value =>
                    value.Contains(family, StringComparison.Ordinal));
                count += node.ResponsiveStyles.Values.Sum(layer => layer.Values.Count(value =>
                    value.Contains(family, StringComparison.Ordinal)));
                foreach (var child in node.Children)
                {
                    stack.Push(child);
                }
            }
        }

        return count;
    }

    /// <summary>Human-readable binary size for the asset details UI.</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes.ToString(CultureInfo.InvariantCulture)} B";
        }

        var value = bytes / 1024d;
        var unit = "KB";
        if (value >= 1024)
        {
            value /= 1024;
            unit = "MB";
        }

        if (value >= 1024)
        {
            value /= 1024;
            unit = "GB";
        }

        return $"{value.ToString("0.#", CultureInfo.InvariantCulture)} {unit}";
    }
}
