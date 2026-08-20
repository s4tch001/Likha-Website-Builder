using Microsoft.Win32;

namespace WebsiteBuilder.App.Services;

/// <inheritdoc cref="IFileDialogService" />
public sealed class FileDialogService : IFileDialogService
{
    public string? PromptOpen(string filter, string title)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title,
            CheckFileExists = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public IReadOnlyList<string> PromptOpenFiles(string filter, string title)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title,
            CheckFileExists = true,
            Multiselect = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : Array.Empty<string>();
    }

    public string? PromptSave(string filter, string title, string defaultFileName, string defaultExtension)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            Title = title,
            FileName = defaultFileName,
            DefaultExt = defaultExtension,
            AddExtension = true,
            OverwritePrompt = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PromptFolder(string title, string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
        };

        if (!string.IsNullOrEmpty(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
