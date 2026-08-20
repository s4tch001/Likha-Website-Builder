using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteBuilder.App.Models;
using WebsiteBuilder.App.Services;

namespace WebsiteBuilder.App.ViewModels.Panels;

/// <summary>
/// The Components toolbox: the full, searchable palette of draggable elements
/// grouped by category (data from <see cref="ComponentCatalog"/>). Drag-to-canvas
/// wiring lands in Phase 5; the catalogue and its live search filter are complete.
/// </summary>
public sealed partial class ComponentsViewModel : ToolViewModel
{
    private readonly EditorSession _editor;

    public ComponentsViewModel(EditorSession editor)
        : base(PanelIds.Components, "Components")
    {
        _editor = editor;
        ApplyFilter();
    }

    public ObservableCollection<ComponentGroup> Groups { get; } = new();

    /// <summary>Inserts the clicked component onto the editor canvas via the bridge.</summary>
    [RelayCommand]
    private void Insert(ComponentItem? item)
    {
        if (item is not null)
        {
            _editor.InsertElement(item.ElementType);
        }
    }

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Groups.Clear();

        var query = SearchText?.Trim();
        foreach (var group in ComponentCatalog.Groups)
        {
            var items = string.IsNullOrEmpty(query)
                ? group.Items
                : group.Items
                    .Where(i => i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (items.Count > 0)
            {
                Groups.Add(items == group.Items ? group : new ComponentGroup(group.Name, items));
            }
        }
    }
}
