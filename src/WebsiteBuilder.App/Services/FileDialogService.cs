using Microsoft.Win32;
using System.Windows;

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

    public UnsavedChangesChoice PromptUnsavedChanges(string projectName, string action)
    {
        var result = MessageBox.Show(
            $"Save changes to '{projectName}' before {action}?",
            "Unsaved changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        return result switch
        {
            MessageBoxResult.Yes => UnsavedChangesChoice.Save,
            MessageBoxResult.No => UnsavedChangesChoice.Discard,
            _ => UnsavedChangesChoice.Cancel,
        };
    }

    public bool PromptRestoreRecovery() => MessageBox.Show(
        "Likha found an unsaved recovery copy from the previous session. Restore it now?",
        "Restore recovered project",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question) == MessageBoxResult.Yes;
}
