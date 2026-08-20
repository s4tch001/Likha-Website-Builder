using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteBuilder.App.Commands;
using WebsiteBuilder.App.Services;
using WebsiteBuilder.App.ViewModels.Panels;
using WebsiteBuilder.CodeGen;
using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Services;

namespace WebsiteBuilder.App.ViewModels;

/// <summary>
/// Orchestrates the main shell: owns the dockable panel view models, the central
/// canvas document and the command palette, and builds the shared command
/// registry that drives the ribbon, the palette and keyboard shortcuts. View-only
/// layout actions are delegated to the window through <see cref="IShellLayout"/>.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IProjectService _projects;
    private readonly ICommandRegistry _registry;
    private readonly IFileDialogService _fileDialogs;
    private readonly EditorSession _editor;
    private readonly AutoSaveService _autoSave;
    private readonly ProjectExportService _exporter;
    private readonly PreviewService _preview;
    private readonly Dispatcher _uiDispatcher = Dispatcher.CurrentDispatcher;

    private IShellLayout? _shellLayout;
    private IRelayCommand? _undoCommand;
    private IRelayCommand? _redoCommand;
    private IRelayCommand? _deleteCommand;
    private IRelayCommand? _duplicateCommand;
    private IRelayCommand? _copyCommand;
    private IRelayCommand? _pasteCommand;
    private readonly List<IRelayCommand> _arrangeCommands = new();

    private const string OpenFilter =
        "WebsiteBuilder Project (project.json;*.wbproj)|project.json;*.wbproj|All files (*.*)|*.*";

    public MainWindowViewModel(
        IProjectService projects,
        ICommandRegistry registry,
        IFileDialogService fileDialogs,
        EditorSession editor,
        AutoSaveService autoSave,
        ProjectExportService exporter,
        PreviewService preview,
        CommandPaletteViewModel commandPalette,
        CanvasViewModel canvas,
        ProjectExplorerViewModel projectExplorer,
        LayersViewModel layers,
        ComponentsViewModel components,
        AssetsViewModel assets,
        PropertyInspectorViewModel propertyInspector,
        FileManagerViewModel fileManager)
    {
        _projects = projects;
        _registry = registry;
        _fileDialogs = fileDialogs;
        _editor = editor;
        _autoSave = autoSave;
        _exporter = exporter;
        _preview = preview;

        CommandPalette = commandPalette;
        Canvas = canvas;
        ProjectExplorer = projectExplorer;
        Layers = layers;
        Components = components;
        Assets = assets;
        PropertyInspector = propertyInspector;
        FileManager = fileManager;

        _projects.CurrentChanged += (_, project) => OnProjectChanged(project);
        _projects.DirtyChanged += (_, _) => RefreshTitle();
        _editor.HistoryChanged += (_, _) => RefreshUndoRedo();
        _editor.SelectionChanged += (_, _) => RefreshSelectionCommands();
        _editor.ClipboardChanged += (_, _) => RefreshClipboardCommands();
        _autoSave.AutoSaved += (_, message) => StatusMessage = message;
        _preview.StateChanged += (_, _) => UpdatePreviewButton();

        BuildCommands();

        // Ensure there is always an open project so the panels and canvas have
        // an authoritative model. New projects intentionally start with a blank page.
        if (_projects.Current is null)
        {
            var restored = false;
            if (_autoSave.HasRecovery && _autoSave.TryReadRecovery(out var recovery) && recovery is not null)
            {
                if (_fileDialogs.PromptRestoreRecovery())
                {
                    _projects.ApplyHostUpdate(recovery);
                    restored = true;
                    StatusMessage = "Recovered unsaved project.";
                }

                _autoSave.DiscardRecovery();
            }

            if (!restored)
            {
                _projects.New();
            }
        }
        else
        {
            OnProjectChanged(_projects.Current);
        }
    }

    // --- Panels / documents exposed to XAML ---
    public CommandPaletteViewModel CommandPalette { get; }
    public CanvasViewModel Canvas { get; }
    public ProjectExplorerViewModel ProjectExplorer { get; }
    public LayersViewModel Layers { get; }
    public ComponentsViewModel Components { get; }
    public AssetsViewModel Assets { get; }
    public PropertyInspectorViewModel PropertyInspector { get; }
    public FileManagerViewModel FileManager { get; }

    /// <summary>The shared command registry (ribbon and palette bind through this).</summary>
    public ICommandRegistry Registry => _registry;

    public const string AppName = "Likha - Website Builder";

    /// <summary>True when closing or replacing the project requires confirmation.</summary>
    public bool HasUnsavedChanges => _projects.IsDirty;

    [ObservableProperty]
    private string _title = AppName;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _previewButtonText = "Preview";

    /// <summary>Called by the window once the dock layout exists so view commands can target it.</summary>
    public void AttachLayout(IShellLayout shellLayout) => _shellLayout = shellLayout;

    private void BuildCommands()
    {
        // File
        _registry.Register(new AppCommand("file.new", "New", "File", new AsyncRelayCommand(NewProjectAsync), "Ctrl+N", "🗋"));
        _registry.Register(new AppCommand("file.open", "Open", "File", new AsyncRelayCommand(OpenProjectAsync), "Ctrl+O", "📂"));
        _registry.Register(new AppCommand("file.save", "Save", "File", new AsyncRelayCommand(SaveProjectAsync), "Ctrl+S", "💾"));
        _registry.Register(new AppCommand("file.saveAs", "Save As", "File", new AsyncRelayCommand(SaveProjectAsAsync), "Ctrl+Shift+S", "💾"));
        _registry.Register(new AppCommand("file.preview", "Preview / Stop Preview", "File", new AsyncRelayCommand(TogglePreviewAsync), glyph: "▶"));
        _registry.Register(new AppCommand("file.exportHtml", "Export HTML", "File", new AsyncRelayCommand(ExportStaticHtmlAsync), glyph: "⬇"));
        _registry.Register(new AppCommand("file.exportReact", "Export Next.js", "File", new AsyncRelayCommand(ExportReactAsync), glyph: "⚛"));

        // Edit
        _undoCommand = new RelayCommand(_editor.Undo, () => _editor.CanUndo);
        _redoCommand = new RelayCommand(_editor.Redo, () => _editor.CanRedo);
        _registry.Register(new AppCommand("edit.undo", "Undo", "Edit", _undoCommand, "Ctrl+Z", "↶"));
        _registry.Register(new AppCommand("edit.redo", "Redo", "Edit", _redoCommand, "Ctrl+Y", "↷"));

        // Duplicate / Delete operate on the canvas selection (via the editor bridge).
        _duplicateCommand = new RelayCommand(() => _editor.DuplicateSelected(), () => _editor.HasSelection);
        _deleteCommand = new RelayCommand(() => _editor.DeleteSelected(), () => _editor.HasSelection);
        _registry.Register(new AppCommand("edit.duplicate", "Duplicate", "Edit", _duplicateCommand, "Ctrl+D", "⧉"));
        _registry.Register(new AppCommand("edit.delete", "Delete", "Edit", _deleteCommand, "Del", "🗑"));

        _copyCommand = new RelayCommand(_editor.Copy, () => _editor.HasSelection);
        _pasteCommand = new RelayCommand(_editor.Paste, () => _editor.CanPaste);
        _registry.Register(new AppCommand("edit.copy", "Copy", "Edit", _copyCommand, "Ctrl+C", "⧉"));
        _registry.Register(new AppCommand("edit.paste", "Paste", "Edit", _pasteCommand, "Ctrl+V", "📋"));

        // View — panel toggles (delegated to the window's dock layout)
        _registry.Register(AppCommand.Create("view.projectExplorer", "Project", "View", () => TogglePanel(PanelIds.ProjectExplorer), glyph: "🗂"));
        _registry.Register(AppCommand.Create("view.layers", "Layers", "View", () => TogglePanel(PanelIds.Layers), glyph: "☰"));
        _registry.Register(AppCommand.Create("view.components", "Components", "View", () => TogglePanel(PanelIds.Components), glyph: "⬚"));
        _registry.Register(AppCommand.Create("view.assets", "Assets", "View", () => TogglePanel(PanelIds.Assets), glyph: "🖼"));
        _registry.Register(AppCommand.Create("view.properties", "Properties", "View", () => TogglePanel(PanelIds.PropertyInspector), glyph: "⚙"));
        _registry.Register(AppCommand.Create("view.files", "Files", "View", () => TogglePanel(PanelIds.FileManager), glyph: "📁"));
        _registry.Register(AppCommand.Create("view.resetLayout", "Reset Layout", "View", () => _shellLayout?.ResetLayout(), glyph: "⟲"));
        _registry.Register(AppCommand.Create("view.commandPalette", "Command Palette", "View", () => CommandPalette.Toggle(), gestureText: "Ctrl+Shift+P", glyph: "⌕"));

        // View — zoom (forwarded to the canvas document)
        _registry.Register(new AppCommand("view.zoomIn", "Zoom In", "View", Canvas.ZoomInCommand, "Ctrl++", "🔍"));
        _registry.Register(new AppCommand("view.zoomOut", "Zoom Out", "View", Canvas.ZoomOutCommand, "Ctrl+-", "🔍"));
        _registry.Register(new AppCommand("view.zoomReset", "Reset Zoom", "View", Canvas.ZoomResetCommand, "Ctrl+0", "🔍"));

        // Arrange — alignment / distribution (operate on a multi-selection via the editor).
        RegisterArrange("arrange.alignLeft", "Align Left", "left", 2);
        RegisterArrange("arrange.alignCenterH", "Align Center", "hcenter", 2);
        RegisterArrange("arrange.alignRight", "Align Right", "right", 2);
        RegisterArrange("arrange.alignTop", "Align Top", "top", 2);
        RegisterArrange("arrange.alignMiddle", "Align Middle", "vmiddle", 2);
        RegisterArrange("arrange.alignBottom", "Align Bottom", "bottom", 2);
        RegisterArrange("arrange.distributeH", "Distribute H", "distH", 3);
        RegisterArrange("arrange.distributeV", "Distribute V", "distV", 3);

        // Help
        _registry.Register(AppCommand.Create("help.about", "About", "Help", ShowAbout, glyph: "ℹ"));
    }

    private void RegisterArrange(string id, string title, string mode, int minSelection)
    {
        var command = new RelayCommand(() => _editor.Align(mode), () => _editor.SelectedCount >= minSelection);
        _arrangeCommands.Add(command);
        _registry.Register(new AppCommand(id, title, "Arrange", command));
    }

    private async Task NewProjectAsync()
    {
        if (!await ConfirmCanReplaceAsync("creating a new project").ConfigureAwait(true))
        {
            return;
        }

        await StopPreviewAsync().ConfigureAwait(true);
        _projects.New();
        StatusMessage = "Created new project.";
    }

    private async Task OpenProjectAsync()
    {
        if (!await ConfirmCanReplaceAsync("opening another project").ConfigureAwait(true))
        {
            return;
        }

        var path = _fileDialogs.PromptOpen(OpenFilter, "Open Project");
        if (path is null)
        {
            return;
        }

        try
        {
            await StopPreviewAsync().ConfigureAwait(true);
            await _projects.OpenAsync(path).ConfigureAwait(true);
            StatusMessage = $"Opened {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open project: {ex.Message}";
        }
    }

    private async Task SaveProjectAsync()
        => _ = await SaveProjectCoreAsync().ConfigureAwait(true);

    private async Task<bool> SaveProjectCoreAsync()
    {
        if (_projects.CurrentPath is null)
        {
            return await SaveProjectAsCoreAsync().ConfigureAwait(true);
        }

        try
        {
            await _projects.SaveAsync().ConfigureAwait(true);
            StatusMessage = $"Saved {_projects.CurrentPath}";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save project: {ex.Message}";
            return false;
        }
    }

    private async Task SaveProjectAsAsync()
        => _ = await SaveProjectAsCoreAsync().ConfigureAwait(true);

    private async Task<bool> SaveProjectAsCoreAsync()
    {
        var folder = _fileDialogs.PromptFolder(
            "Choose a folder for the project (project.json and asset folders are created here)",
            _projects.ProjectDirectory);
        if (folder is null)
        {
            return false;
        }

        try
        {
            await _projects.SaveToFolderAsync(folder).ConfigureAwait(true);
            RefreshTitle();
            FileManager.Refresh();
            Assets.Refresh();
            StatusMessage = $"Saved to {_projects.ProjectDirectory}";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save project: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Resolves unsaved work before replacing or closing the active project.
    /// </summary>
    public async Task<bool> ConfirmCanReplaceAsync(string action)
    {
        if (!_projects.IsDirty)
        {
            return true;
        }

        return _fileDialogs.PromptUnsavedChanges(_projects.Current?.Name ?? "Untitled Project", action) switch
        {
            UnsavedChangesChoice.Discard => true,
            UnsavedChangesChoice.Save => await SaveProjectCoreAsync().ConfigureAwait(true),
            _ => false,
        };
    }

    private Task ExportStaticHtmlAsync() => ExportAsync(new HtmlCodeGenerator(), "static HTML site");

    private Task ExportReactAsync() => ExportAsync(new ReactCodeGenerator(), "Next.js project");

    private async Task TogglePreviewAsync()
    {
        try
        {
            if (_preview.IsRunning)
            {
                await _preview.StopAsync().ConfigureAwait(true);
                StatusMessage = "Preview stopped.";
            }
            else
            {
                var url = await _preview.StartAsync().ConfigureAwait(true);
                StatusMessage = $"Preview running at {url}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Preview failed: {ex.Message}";
        }
    }

    public Task StopPreviewAsync() => _preview.StopAsync();

    private void UpdatePreviewButton()
    {
        void Update() => PreviewButtonText = _preview.IsRunning ? "Stop Preview" : "Preview";
        if (_uiDispatcher.CheckAccess())
        {
            Update();
        }
        else
        {
            _uiDispatcher.BeginInvoke(Update);
        }
    }

    private async Task ExportAsync(ICodeGenerator generator, string what)
    {
        if (_projects.Current is null)
        {
            return;
        }

        var folder = _fileDialogs.PromptFolder(
            $"Choose a folder to export the {what} into", _projects.ProjectDirectory);
        if (folder is null)
        {
            return;
        }

        try
        {
            var result = await _exporter.ExportAsync(
                generator,
                _projects.Current,
                folder,
                _projects.ProjectDirectory).ConfigureAwait(true);
            StatusMessage = $"Exported {result.GeneratedFileCount} generated files and {result.AssetCount} assets to {folder}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    private void ShowAbout()
    {
        var version = typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3) ?? "unknown";
        StatusMessage = $"{AppName} {version} — visual website builder.";
    }

    private void TogglePanel(string contentId) => _shellLayout?.TogglePanel(contentId);

    private void OnProjectChanged(Project project)
    {
        RefreshTitle();
        StatusMessage = $"Project: {project.Name}";
    }

    private void RefreshTitle() => Title = BuildTitle(_projects.Current, _projects.IsDirty);

    private void RefreshUndoRedo()
    {
        _undoCommand?.NotifyCanExecuteChanged();
        _redoCommand?.NotifyCanExecuteChanged();
    }

    private void RefreshSelectionCommands()
    {
        _copyCommand?.NotifyCanExecuteChanged();
        _deleteCommand?.NotifyCanExecuteChanged();
        _duplicateCommand?.NotifyCanExecuteChanged();
        foreach (var command in _arrangeCommands)
        {
            command.NotifyCanExecuteChanged();
        }
    }

    private void RefreshClipboardCommands() => _pasteCommand?.NotifyCanExecuteChanged();

    private static string BuildTitle(Project? project, bool dirty) =>
        project is null ? AppName : $"{(dirty ? "● " : string.Empty)}{project.Name} — {AppName}";
}
