using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Serialization;

namespace WebsiteBuilder.Core.Services;

/// <summary>
/// File-backed implementation of <see cref="IProjectService"/>. Persistence uses
/// the shared <see cref="ProjectSerializer"/>, so saved files are the canonical
/// Project JSON the rest of the toolchain consumes. The richer folder-based
/// project layout and auto-save arrive in Phase 10; this already provides fully
/// working New / Open / Save / Save As over a single <c>.wbproj</c> JSON file.
/// </summary>
public sealed class ProjectService : IProjectService
{
    /// <summary>Legacy single-file extension (still openable for back-compat).</summary>
    public const string FileExtension = ".wbproj";

    /// <summary>Canonical model file name inside a folder-based project.</summary>
    public const string ProjectFileName = "project.json";

    /// <summary>Standard subfolders scaffolded inside a folder-based project.</summary>
    public static IReadOnlyList<string> StandardFolders { get; } =
        new[] { "Assets", "Components", "Pages", "Styles", "Scripts" };

    private Project? _current;
    private string? _currentPath;
    private bool _isDirty;

    /// <inheritdoc />
    public Project? Current => _current;

    /// <inheritdoc />
    public bool IsDirty => _isDirty;

    /// <inheritdoc />
    public event EventHandler? DirtyChanged;

    /// <inheritdoc />
    public string? CurrentPath => _currentPath;

    /// <inheritdoc />
    public string? ProjectDirectory =>
        _currentPath is null ? null : Path.GetDirectoryName(_currentPath);

    /// <inheritdoc />
    public event EventHandler<Project>? CurrentChanged;

    /// <inheritdoc />
    public event EventHandler<Project>? Mutated;

    /// <inheritdoc />
    public Project New(string name = "Untitled Project")
    {
        var project = Project.CreateDefault(name);
        SetCurrent(project, path: null);
        return project;
    }

    /// <inheritdoc />
    public async Task<Project> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Accept a project folder, a project.json, or a legacy single .wbproj file.
        var filePath = Directory.Exists(path) ? Path.Combine(path, ProjectFileName) : path;
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"No project found at '{path}'.", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        var project = ProjectSerializer.Deserialize(json);
        SetCurrent(project, filePath);
        return project;
    }

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (_currentPath is null)
        {
            throw new InvalidOperationException(
                "No file path is associated with the current project. Use SaveAsAsync first.");
        }

        await WriteAsync(_currentPath, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await WriteAsync(path, cancellationToken).ConfigureAwait(false);
        _currentPath = path;
    }

    /// <inheritdoc />
    public async Task SaveToFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        Directory.CreateDirectory(folderPath);
        var filePath = Path.Combine(folderPath, ProjectFileName);
        await WriteAsync(filePath, cancellationToken).ConfigureAwait(false);
        _currentPath = filePath;
    }

    private async Task WriteAsync(string path, CancellationToken cancellationToken)
    {
        var project = _current
            ?? throw new InvalidOperationException("There is no current project to save.");

        project.ModifiedUtc = DateTimeOffset.UtcNow;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);

            // A folder-based project (project.json) gets the standard layout scaffolded
            // alongside it so later phases (assets, components, exports) have a home.
            if (string.Equals(Path.GetFileName(path), ProjectFileName, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var folder in StandardFolders)
                {
                    Directory.CreateDirectory(Path.Combine(directory, folder));
                }
            }
        }

        var json = ProjectSerializer.Serialize(project);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
        SetDirty(false);
    }

    /// <inheritdoc />
    public void ApplyEditorUpdate(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        _current = project;
        SetDirty(true);
        Mutated?.Invoke(this, project);
    }

    private void SetCurrent(Project project, string? path)
    {
        _current = project;
        _currentPath = path;
        SetDirty(false);
        CurrentChanged?.Invoke(this, project);
    }

    private void SetDirty(bool value)
    {
        if (_isDirty == value)
        {
            return;
        }

        _isDirty = value;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }
}
