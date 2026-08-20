using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteBuilder.App.Services;
using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Services;

namespace WebsiteBuilder.App.ViewModels.Panels;

/// <summary>A managed project asset displayed by the Assets panel.</summary>
public sealed record AssetItem(ProjectAsset Asset, string FullPath)
{
    public string Name => Asset.Name;
    public string Kind => Asset.Kind;
    public string RelativePath => Asset.RelativePath;
}

/// <summary>
/// Displays managed project assets and delegates all import/delete filesystem
/// operations to the validated Core asset service. Preview and canvas dragging
/// are intentionally deferred to later Phase 13 sub-phases.
/// </summary>
public sealed partial class AssetsViewModel : ToolViewModel
{
    private const string ImportFilter =
        "All supported assets|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp;*.avif;*.svg;*.ico;*.mp4;*.webm;*.mov;*.ogv;*.mp3;*.wav;*.ogg;*.m4a;*.woff;*.woff2;*.ttf;*.otf;*.pdf;*.txt;*.md;*.json" +
        "|Images and SVG|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp;*.avif;*.svg;*.ico" +
        "|Video and audio|*.mp4;*.webm;*.mov;*.ogv;*.mp3;*.wav;*.ogg;*.m4a" +
        "|Fonts|*.woff;*.woff2;*.ttf;*.otf" +
        "|Documents|*.pdf;*.txt;*.md;*.json";

    private readonly IProjectService _projects;
    private readonly IFileDialogService _fileDialogs;
    private readonly IAssetService _assetService;

    public AssetsViewModel(
        IProjectService projects,
        IFileDialogService fileDialogs,
        IAssetService assetService)
        : base(PanelIds.Assets, "Assets")
    {
        _projects = projects;
        _fileDialogs = fileDialogs;
        _assetService = assetService;
        _projects.CurrentChanged += (_, _) => Refresh();
        Refresh();
    }

    public ObservableCollection<AssetItem> Assets { get; } = new();

    public bool HasAssets => Assets.Count > 0;

    /// <summary>Status line shown above the list.</summary>
    [ObservableProperty]
    private string _status = "No project folder yet — save the project to import assets.";

    /// <summary>Rebuilds the list from canonical project metadata.</summary>
    public void Refresh()
    {
        Assets.Clear();

        if (_projects.ProjectDirectory is not { } directory || _projects.Current is not { } project)
        {
            Status = "No project folder yet — save the project to import assets.";
            OnPropertyChanged(nameof(HasAssets));
            return;
        }

        var unavailable = 0;
        foreach (var asset in project.Assets.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (_assetService.TryGetFullPath(directory, asset, out var fullPath) && File.Exists(fullPath))
            {
                Assets.Add(new AssetItem(asset, fullPath));
            }
            else
            {
                unavailable++;
            }
        }

        Status = Assets.Count == 0
            ? "No assets yet. Use Import to add files."
            : $"{Assets.Count} asset(s).";
        if (unavailable > 0)
        {
            Status += $" {unavailable} unavailable metadata entr{(unavailable == 1 ? "y" : "ies")}.";
        }

        OnPropertyChanged(nameof(HasAssets));
    }

    /// <summary>Imports one or more files through the bounded Core pipeline.</summary>
    [RelayCommand]
    private async Task ImportAsync(CancellationToken cancellationToken)
    {
        if (_projects.ProjectDirectory is not { } directory || _projects.Current is not { } project)
        {
            Status = "Save the project first, then import assets.";
            return;
        }

        var files = _fileDialogs.PromptOpenFiles(ImportFilter, "Import assets");
        if (files.Count == 0)
        {
            return;
        }

        var imported = 0;
        var failures = new List<string>();
        foreach (var source in files)
        {
            var result = await _assetService.ImportAsync(project, directory, source, cancellationToken)
                .ConfigureAwait(true);
            if (result.IsSuccess)
            {
                imported++;
            }
            else
            {
                failures.Add($"{Path.GetFileName(source)}: {result.Message}");
            }
        }

        if (imported > 0)
        {
            _projects.ApplyEditorUpdate(project);
        }

        Refresh();
        Status = failures.Count == 0
            ? $"Imported {imported} asset(s)."
            : $"Imported {imported}; {failures.Count} failed. {failures[0]}";
    }

    /// <summary>Deletes only the managed file represented by canonical metadata.</summary>
    [RelayCommand]
    private async Task DeleteAsync(AssetItem? item, CancellationToken cancellationToken)
    {
        if (item is null
            || _projects.ProjectDirectory is not { } directory
            || _projects.Current is not { } project)
        {
            return;
        }

        var result = await _assetService.DeleteAsync(
            project,
            directory,
            item.Asset,
            cancellationToken).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            Status = result.Message;
            return;
        }

        _projects.ApplyEditorUpdate(project);
        Refresh();
        Status = $"Deleted {item.Name}.";
    }
}
