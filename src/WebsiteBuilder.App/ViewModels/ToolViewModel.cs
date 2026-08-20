using CommunityToolkit.Mvvm.ComponentModel;

namespace WebsiteBuilder.App.ViewModels;

/// <summary>
/// Base class for dockable tool-panel view models. Carries the metadata
/// AvalonDock needs (a title and a stable ContentId used for layout persistence
/// and show/hide targeting) plus a visibility flag bound to the dock item.
/// </summary>
public abstract partial class ToolViewModel : ObservableObject
{
    protected ToolViewModel(string contentId, string title)
    {
        ContentId = contentId;
        _title = title;
    }

    /// <summary>Stable identifier for this panel (e.g. "panel.layers").</summary>
    public string ContentId { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isVisible = true;
}
