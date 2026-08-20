namespace WebsiteBuilder.App.Services;

/// <summary>
/// Abstracts the native open/save file dialogs so view models stay testable and
/// free of direct <c>Microsoft.Win32</c> dependencies.
/// </summary>
public interface IFileDialogService
{
    /// <summary>Shows an Open dialog. Returns the chosen path, or null if cancelled.</summary>
    string? PromptOpen(string filter, string title);

    /// <summary>Shows a multi-select Open dialog. Returns the chosen paths (empty if cancelled).</summary>
    IReadOnlyList<string> PromptOpenFiles(string filter, string title);

    /// <summary>Shows a Save dialog. Returns the chosen path, or null if cancelled.</summary>
    string? PromptSave(string filter, string title, string defaultFileName, string defaultExtension);

    /// <summary>Shows a folder-picker dialog. Returns the chosen folder, or null if cancelled.</summary>
    string? PromptFolder(string title, string? initialDirectory = null);
}
