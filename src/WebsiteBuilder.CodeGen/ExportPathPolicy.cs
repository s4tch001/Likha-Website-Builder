namespace WebsiteBuilder.CodeGen;

/// <summary>Resolves generated relative paths without allowing export-root escape.</summary>
public static class ExportPathPolicy
{
    /// <summary>Returns a contained absolute target path or throws for an unsafe path.</summary>
    public static string ResolveContainedPath(string exportRoot, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException($"Generated path '{relativePath}' must be relative.");
        }

        var root = Path.GetFullPath(exportRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalized = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var target = Path.GetFullPath(Path.Combine(root, normalized));
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || string.Equals(target, root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Generated path '{relativePath}' escapes the export folder.");
        }

        return target;
    }
}
