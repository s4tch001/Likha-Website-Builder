using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Services;

namespace WebsiteBuilder.App.ViewModels.Panels;

/// <summary>An entry in the File Manager tree (a folder or a file on disk).</summary>
public sealed class FileEntry
{
    public FileEntry(string name, bool isDirectory)
    {
        Name = name;
        IsDirectory = isDirectory;
        Children = new ObservableCollection<FileEntry>();
    }

    public string Name { get; }
    public bool IsDirectory { get; }
    public ObservableCollection<FileEntry> Children { get; }
}

/// <summary>
/// Shows the on-disk folder of the saved project. Create / rename / move /
/// import operations arrive in Phase 13; the live tree of the project directory
/// is real and refreshes whenever the project is saved or opened.
/// </summary>
public sealed partial class FileManagerViewModel : ToolViewModel
{
    private readonly IProjectService _projects;

    public FileManagerViewModel(IProjectService projects)
        : base(PanelIds.FileManager, "Files")
    {
        _projects = projects;
        _projects.CurrentChanged += (_, _) => Refresh();
        Refresh();
    }

    public ObservableCollection<FileEntry> Entries { get; } = new();

    [ObservableProperty]
    private string _rootLabel = "Unsaved project";

    public void Refresh()
    {
        Entries.Clear();

        var path = _projects.CurrentPath;
        if (path is null)
        {
            RootLabel = "Unsaved project";
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            RootLabel = "Unsaved project";
            return;
        }

        RootLabel = directory;
        foreach (var entry in BuildEntries(directory))
        {
            Entries.Add(entry);
        }
    }

    private static IEnumerable<FileEntry> BuildEntries(string directory)
    {
        foreach (var dir in Directory.EnumerateDirectories(directory).OrderBy(Path.GetFileName))
        {
            var node = new FileEntry(Path.GetFileName(dir), isDirectory: true);
            foreach (var child in BuildEntries(dir))
            {
                node.Children.Add(child);
            }

            yield return node;
        }

        foreach (var file in Directory.EnumerateFiles(directory).OrderBy(Path.GetFileName))
        {
            yield return new FileEntry(Path.GetFileName(file), isDirectory: false);
        }
    }
}
