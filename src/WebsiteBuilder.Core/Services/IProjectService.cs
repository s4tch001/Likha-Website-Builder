using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.Core.Services;

/// <summary>
/// Owns the lifecycle of the in-memory <see cref="Project"/>: new/open/save and
/// notifications when the active project changes. The full file-backed
/// implementation lands in Phase 10 (JSON serializer); this contract is stable
/// from Phase 1 so dependents can be wired up now.
/// </summary>
public interface IProjectService
{
    /// <summary>The currently open project, or <c>null</c> if none is loaded.</summary>
    Project? Current { get; }

    /// <summary>Absolute path of the open project file, or <c>null</c> if never saved.</summary>
    string? CurrentPath { get; }

    /// <summary>
    /// Folder that contains the open project, or <c>null</c> if never saved. For a
    /// folder-based project this is the project directory; for a legacy single-file
    /// project it is the file's directory.
    /// </summary>
    string? ProjectDirectory { get; }

    /// <summary>True when the current project has unsaved in-memory changes.</summary>
    bool IsDirty { get; }

    /// <summary>
    /// Monotonically increasing in-memory model revision. Every replacement or
    /// accepted mutation advances it so bridge peers can reject stale snapshots.
    /// </summary>
    long Revision { get; }

    /// <summary>Raised whenever <see cref="IsDirty"/> changes.</summary>
    event EventHandler? DirtyChanged;

    /// <summary>Raised whenever <see cref="Current"/> is replaced (new/open).</summary>
    event EventHandler<Project>? CurrentChanged;

    /// <summary>
    /// Raised when the current project is mutated in place (e.g. an edit pushed
    /// from the editor) rather than replaced. Lets panels refresh without the
    /// host re-pushing the project back to the editor.
    /// </summary>
    event EventHandler<Project>? Mutated;

    /// <summary>
    /// Raised after a host-originated mutation that must also be published to the
    /// embedded editor. Editor-originated updates never raise this event.
    /// </summary>
    event EventHandler<Project>? HostMutated;

    /// <summary>Creates a fresh project and makes it current.</summary>
    Project New(string name = "Untitled Project");

    /// <summary>Loads a project from disk and makes it current.</summary>
    Task<Project> OpenAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Writes the current project to its existing path.</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the current project to a new path and adopts it.</summary>
    Task SaveAsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the current project into <paramref name="folderPath"/> as a folder-based
    /// project: writes <c>project.json</c> and scaffolds the standard subfolders
    /// (Assets/Components/Pages/Styles/Scripts), then adopts that location.
    /// </summary>
    Task SaveToFolderAsync(string folderPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the current project with an edited copy received from the editor,
    /// raising <see cref="Mutated"/> (not <see cref="CurrentChanged"/>) so the
    /// change is not echoed back to the editor.
    /// </summary>
    void ApplyEditorUpdate(Project project);

    /// <summary>
    /// Applies an editor snapshot only when it was based on the current revision.
    /// Returns false without changing the project when the snapshot is stale.
    /// </summary>
    bool TryApplyEditorUpdate(Project project, long expectedRevision, out long revision);

    /// <summary>
    /// Applies a host-originated mutation, marks the project dirty, and requests
    /// that the new authoritative snapshot be published to the editor.
    /// </summary>
    void ApplyHostUpdate(Project project);
}
