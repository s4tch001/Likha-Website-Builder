using System.IO;
using WebsiteBuilder.CodeGen;
using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Services;

namespace WebsiteBuilder.App.Services;

/// <summary>Materializes validated generator output and managed assets inside one contained root.</summary>
public sealed class ProjectExportService(IAssetService assetService)
{
    public async Task<ProjectExportResult> ExportAsync(
        ICodeGenerator generator,
        Project project,
        string outputRoot,
        string? projectDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);

        var files = generator.Generate(project);
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var generatedTargets = new List<(GeneratedFile File, string FullPath)>();
        foreach (var file in files)
        {
            var fullPath = ExportPathPolicy.ResolveContainedPath(outputRoot, file.RelativePath);
            if (!targets.Add(fullPath))
            {
                throw new InvalidDataException($"The generator produced duplicate output '{file.RelativePath}'.");
            }

            generatedTargets.Add((file, fullPath));
        }

        var assetCopies = new List<(string SourcePath, string OutputPath)>();
        if (projectDirectory is null)
        {
            if (project.Assets.Count > 0)
            {
                throw new InvalidOperationException("Save the project before exporting managed assets.");
            }
        }
        else
        {
            var assetRoot = generator.Target == CodeGenTarget.React ? "public/Assets" : "Assets";
            foreach (var asset in project.Assets)
            {
                if (!assetService.TryGetFullPath(projectDirectory, asset, out var sourcePath)
                    || !File.Exists(sourcePath))
                {
                    throw new FileNotFoundException($"Managed asset '{asset.Name}' is unavailable.");
                }

                var outputPath = ExportPathPolicy.ResolveContainedPath(
                    outputRoot,
                    $"{assetRoot}/{asset.StoredFileName}");
                if (!targets.Add(outputPath))
                {
                    throw new InvalidDataException($"Duplicate asset output '{asset.StoredFileName}'.");
                }

                assetCopies.Add((sourcePath, outputPath));
            }
        }

        // Resolve and validate the complete output plan before touching the destination.
        foreach (var (file, fullPath) in generatedTargets)
        {
            await AtomicFileWriter.WriteAllTextAsync(
                fullPath,
                file.Contents,
                createBackup: false,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var (sourcePath, outputPath) in assetCopies)
        {
            await AtomicFileWriter.CopyFileAsync(
                sourcePath,
                outputPath,
                createBackup: false,
                cancellationToken).ConfigureAwait(false);
        }

        return new ProjectExportResult(files.Count, assetCopies.Count);
    }
}

public readonly record struct ProjectExportResult(int GeneratedFileCount, int AssetCount);
