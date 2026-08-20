using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
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
    public string MediaType => Asset.MediaType;
    public string Size => ProjectAssetPolicy.FormatSize(Asset.SizeBytes);
    public string Imported => Asset.ImportedUtc.LocalDateTime.ToString("g");
    public string Digest => Asset.Sha256;
    public string Glyph => Kind switch
    {
        AssetKinds.Image or AssetKinds.Svg or AssetKinds.Icon => "▧",
        AssetKinds.Video => "▶",
        AssetKinds.Audio => "♫",
        AssetKinds.Font => "Aa",
        _ => "▤",
    };
    public bool CanPreview => Kind is AssetKinds.Image or AssetKinds.Icon
        && Path.GetExtension(FullPath).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico";
    public string ActionLabel => Kind == AssetKinds.Font ? "Apply font" : "Insert";
}

/// <summary>
/// Browses, filters and safely uses managed project assets. File operations remain
/// behind the validated Core service; the UI only passes canonical metadata.
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
    private readonly EditorSession _editor;

    public AssetsViewModel(
        IProjectService projects,
        IFileDialogService fileDialogs,
        IAssetService assetService,
        EditorSession editor)
        : base(PanelIds.Assets, "Assets")
    {
        _projects = projects;
        _fileDialogs = fileDialogs;
        _assetService = assetService;
        _editor = editor;
        FilteredAssets = CollectionViewSource.GetDefaultView(Assets);
        FilteredAssets.Filter = FilterAsset;
        _projects.CurrentChanged += (_, _) => Refresh();
        _projects.Mutated += (_, _) => Refresh();
        Refresh();
    }

    public ObservableCollection<AssetItem> Assets { get; } = new();

    public ICollectionView FilteredAssets { get; }

    public IReadOnlyList<string> Categories { get; } =
        ["All", AssetKinds.Image, AssetKinds.Svg, AssetKinds.Icon, AssetKinds.Video,
         AssetKinds.Audio, AssetKinds.Font, AssetKinds.Document];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private AssetItem? _selectedAsset;

    public bool HasSelectedAsset => SelectedAsset is not null;

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
        FilteredAssets.Refresh();
    }

    partial void OnSearchTextChanged(string value) => FilteredAssets.Refresh();

    partial void OnSelectedCategoryChanged(string value) => FilteredAssets.Refresh();

    partial void OnSelectedAssetChanged(AssetItem? value) => OnPropertyChanged(nameof(HasSelectedAsset));

    private bool FilterAsset(object candidate)
    {
        if (candidate is not AssetItem item)
        {
            return false;
        }

        var categoryMatches = SelectedCategory == "All"
            || string.Equals(item.Kind, SelectedCategory, StringComparison.Ordinal);
        var searchMatches = string.IsNullOrWhiteSpace(SearchText)
            || item.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)
            || item.Kind.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)
            || item.MediaType.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase);
        return categoryMatches && searchMatches;
    }

    /// <summary>Inserts media/document assets, or applies a managed font to the selection.</summary>
    [RelayCommand]
    private void Use(AssetItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.Kind == AssetKinds.Font)
        {
            if (_editor.SelectedId is not { } selectedId)
            {
                Status = "Select an element before applying a font.";
                return;
            }

            _editor.SetStyle(selectedId, "font-family", $"'{ProjectAssetPolicy.FontFamily(item.Asset)}'");
            Status = $"Applied {item.Name} to the selected element.";
            return;
        }

        _editor.InsertAsset(item.Asset);
        Status = $"Inserted {item.Name}.";
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
            _projects.ApplyHostUpdate(project);
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

        var references = ProjectAssetPolicy.CountReferences(project, item.Asset);
        if (references > 0)
        {
            Status = $"Cannot delete {item.Name}: used by {references} element reference(s).";
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

        _projects.ApplyHostUpdate(project);
        Refresh();
        Status = $"Deleted {item.Name}.";
    }
}
