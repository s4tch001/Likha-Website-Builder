using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;
using Microsoft.Web.WebView2.Wpf;
using WebsiteBuilder.App.Services;
using WebsiteBuilder.App.ViewModels;

namespace WebsiteBuilder.App.Views;

/// <summary>
/// The main shell window. Implements <see cref="IShellLayout"/> so the view model
/// can drive AvalonDock panel visibility without taking a dependency on the
/// docking controls (the layout is a pure view concern). Also persists the dock
/// layout across runs and works around the WebView2 "airspace" issue that would
/// otherwise hide auto-hide flyouts behind the native canvas.
/// </summary>
public partial class MainWindow : Window, IShellLayout
{
    private readonly MainWindowViewModel _viewModel;
    private Dictionary<string, LayoutAnchorable>? _panels;

    // Content (the panel UserControls) keyed by ContentId, captured from the XAML
    // layout so it can be reconnected after a serialized layout is restored.
    private readonly Dictionary<string, object> _contentById = new(StringComparer.Ordinal);

    // The pristine XAML layout, captured at startup so "Reset Layout" can restore it.
    private string? _defaultLayoutXml;

    // Airspace workaround state.
    private LayoutAutoHideWindowControl? _autoHide;
    private WebView2? _canvasWeb;
    private bool _closePromptActive;
    private bool _closeApproved;

    private static string LayoutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WebsiteBuilder", "layout.xml");

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CaptureContentMap();
        _defaultLayoutXml = SerializeLayout();

        if (File.Exists(LayoutPath))
        {
            try
            {
                using var reader = new StreamReader(LayoutPath);
                ApplyLayout(reader);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Xml.XmlException)
            {
                // Corrupt/incompatible layout: fall back to the default.
            }
        }

        RebuildPanelMap();

        // Hook the auto-hide flyout so we can hide the WebView2 while it is shown.
        DockManager.LayoutUpdated += OnDockLayoutUpdated;

        _viewModel.AttachLayout(this);
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_closeApproved && _viewModel.HasUnsavedChanges)
        {
            e.Cancel = true;
            if (_closePromptActive)
            {
                return;
            }

            _closePromptActive = true;
            try
            {
                if (await _viewModel.ConfirmCanReplaceAsync("closing Likha").ConfigureAwait(true))
                {
                    _closeApproved = true;
                    Close();
                }
            }
            finally
            {
                _closePromptActive = false;
            }

            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);
            using var writer = new StreamWriter(LayoutPath);
            new XmlLayoutSerializer(DockManager).Serialize(writer);
        }
        catch (IOException)
        {
            // Best-effort; never block shutdown over a layout write.
        }
    }

    // --- Layout serialization helpers ---

    private void CaptureContentMap()
    {
        _contentById.Clear();
        foreach (var content in DockManager.Layout.Descendents().OfType<LayoutContent>())
        {
            if (!string.IsNullOrEmpty(content.ContentId) && content.Content is not null)
            {
                _contentById.TryAdd(content.ContentId, content.Content);
            }
        }
    }

    private string SerializeLayout()
    {
        using var writer = new StringWriter();
        new XmlLayoutSerializer(DockManager).Serialize(writer);
        return writer.ToString();
    }

    private void ApplyLayout(TextReader reader)
    {
        var serializer = new XmlLayoutSerializer(DockManager);
        serializer.LayoutSerializationCallback += (_, args) =>
        {
            if (!string.IsNullOrEmpty(args.Model.ContentId)
                && _contentById.TryGetValue(args.Model.ContentId, out var content))
            {
                args.Content = content;
            }
            else
            {
                args.Cancel = true;
            }
        };
        serializer.Deserialize(reader);
    }

    private void RebuildPanelMap()
    {
        _panels = new Dictionary<string, LayoutAnchorable>(StringComparer.Ordinal);
        foreach (var anchorable in DockManager.Layout.Descendents().OfType<LayoutAnchorable>())
        {
            if (!string.IsNullOrEmpty(anchorable.ContentId))
            {
                _panels.TryAdd(anchorable.ContentId, anchorable);
            }
        }

        foreach (var anchorable in DockManager.Layout.Hidden)
        {
            if (!string.IsNullOrEmpty(anchorable.ContentId))
            {
                _panels.TryAdd(anchorable.ContentId, anchorable);
            }
        }
    }

    // --- WebView2 airspace workaround ---

    private void OnDockLayoutUpdated(object? sender, EventArgs e)
    {
        if (_autoHide is not null)
        {
            return;
        }

        _autoHide = FindDescendant<LayoutAutoHideWindowControl>(DockManager);
        if (_autoHide is not null)
        {
            _autoHide.IsVisibleChanged += (_, _) => UpdateCanvasAirspace();
            UpdateCanvasAirspace();
        }
    }

    private void UpdateCanvasAirspace()
    {
        _canvasWeb ??= FindDescendant<WebView2>(DockManager);
        if (_canvasWeb is not null && _autoHide is not null)
        {
            // Hide the native canvas while a flyout is open so it does not draw
            // over the (WPF) auto-hide panel.
            _canvasWeb.Visibility = _autoHide.IsVisible ? Visibility.Hidden : Visibility.Visible;
        }
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
            {
                return typed;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    // --- IShellLayout ---

    /// <inheritdoc />
    public void TogglePanel(string contentId)
    {
        if (_panels is null || !_panels.TryGetValue(contentId, out var panel))
        {
            return;
        }

        if (panel.IsVisible)
        {
            panel.Hide();
        }
        else
        {
            panel.Show();
        }
    }

    /// <inheritdoc />
    public void ShowPanel(string contentId)
    {
        if (_panels is not null && _panels.TryGetValue(contentId, out var panel))
        {
            panel.Show();
            panel.IsActive = true;
        }
    }

    /// <inheritdoc />
    public void ResetLayout()
    {
        // Restore the pristine startup layout and forget the saved one.
        if (_defaultLayoutXml is not null)
        {
            try
            {
                using var reader = new StringReader(_defaultLayoutXml);
                ApplyLayout(reader);
                RebuildPanelMap();
            }
            catch (InvalidOperationException)
            {
                // Ignore; fall through to simply showing all panels.
            }
        }

        try
        {
            if (File.Exists(LayoutPath))
            {
                File.Delete(LayoutPath);
            }
        }
        catch (IOException)
        {
            // Non-fatal.
        }

        if (_panels is not null)
        {
            foreach (var panel in _panels.Values)
            {
                panel.Show();
            }
        }
    }

    private void PaletteOverlay_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            // Focus the search box as soon as the palette appears.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                PaletteSearch.Focus();
                PaletteSearch.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }
}
