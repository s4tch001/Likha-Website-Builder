using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteBuilder.App.Services;
using WebsiteBuilder.Core.Models;

namespace WebsiteBuilder.App.ViewModels;

/// <summary>
/// The central editing document. Owns the responsive toolbar state (active
/// breakpoint) and the canvas zoom level. The WebView2-hosted React canvas is
/// embedded into this document's view in Phase 3; the surrounding chrome
/// (breakpoint switcher, zoom controls) is fully functional here.
/// </summary>
public sealed partial class CanvasViewModel : ObservableObject
{
    private const double MinZoom = 10;
    private const double MaxZoom = 400;

    private readonly EditorSession _editorSession;

    public CanvasViewModel(EditorSession editorSession)
    {
        _editorSession = editorSession;

        foreach (var breakpoint in Breakpoint.Defaults)
        {
            Breakpoints.Add(breakpoint);
        }

        _activeBreakpoint = Breakpoints.FirstOrDefault();

        _editorStatus = editorSession.Status;
        editorSession.StatusChanged += (_, status) => EditorStatus = status;

        // The editor canvas is the source of truth for zoom; reflect its changes.
        editorSession.ZoomChanged += (_, zoom) => SetProperty(ref _zoom, Math.Round(zoom), nameof(Zoom));
    }

    public string Title => "Canvas";

    /// <summary>Live status of the WebView2 editor connection, shown on the canvas toolbar.</summary>
    [ObservableProperty]
    private string _editorStatus;

    public ObservableCollection<Breakpoint> Breakpoints { get; } = new();

    [ObservableProperty]
    private Breakpoint? _activeBreakpoint;

    [ObservableProperty]
    private double _zoom = 100;

    [RelayCommand]
    private void ZoomIn() => RequestZoom(Math.Min(MaxZoom, Zoom + 10));

    [RelayCommand]
    private void ZoomOut() => RequestZoom(Math.Max(MinZoom, Zoom - 10));

    [RelayCommand]
    private void ZoomReset() => RequestZoom(100);

    [RelayCommand]
    private void SetBreakpoint(Breakpoint? breakpoint)
    {
        if (breakpoint is null)
        {
            return;
        }

        ActiveBreakpoint = breakpoint;
        _editorSession.SetBreakpoint(breakpoint.Id);
    }

    /// <summary>
    /// Sends the desired zoom to the editor. The editor applies it and echoes back
    /// an <c>editor.viewChanged</c> event, which updates <see cref="Zoom"/> — so the
    /// editor canvas remains the single source of truth and there is no feedback loop.
    /// </summary>
    private void RequestZoom(double zoomPercent)
    {
        if (_editorSession.Bridge is null)
        {
            // Editor not connected yet: update the display directly.
            Zoom = zoomPercent;
            return;
        }

        _editorSession.SetZoom(zoomPercent);
    }
}
