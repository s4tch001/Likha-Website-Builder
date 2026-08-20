using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteBuilder.App.Services;
using WebsiteBuilder.Core.Models;
using WebsiteBuilder.Core.Services;

namespace WebsiteBuilder.App.ViewModels.Panels;

/// <summary>Where a dragged layer is dropped relative to the target row.</summary>
public enum LayerDropPosition
{
    Before,
    Inside,
    After,
}

/// <summary>
/// A node shown in the Layers tree. Wraps an <see cref="ElementNode"/> and exposes
/// its children recursively for the <c>TreeView</c>, plus editor-facing state
/// (expand/collapse, selection, hidden/locked, inline rename) bound to the UI.
/// </summary>
public sealed partial class LayerNode : ObservableObject
{
    private readonly LayersViewModel _owner;

    public LayerNode(ElementNode element, LayersViewModel owner)
    {
        _owner = owner;
        Element = element;
        Children = new ObservableCollection<LayerNode>(element.Children.Select(c => new LayerNode(c, owner)));
        _isHidden = element.Hidden;
        _isLocked = element.Locked;
    }

    public ElementNode Element { get; }

    public string Id => Element.Id;

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Element.Name) ? Element.Name! :
        !string.IsNullOrWhiteSpace(Element.Id) ? $"{Element.Type} · {Element.Id}" :
        Element.Type;

    public ObservableCollection<LayerNode> Children { get; }

    public bool HasChildren => Children.Count > 0;

    /// <summary>Whether this branch is expanded in the tree (defaults to expanded).</summary>
    [ObservableProperty]
    private bool _isExpanded = true;

    /// <summary>Whether this node is the selected one. Two-way bound to the tree item.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Mirror of <see cref="ElementNode.Hidden"/>; drives the eye toggle.</summary>
    [ObservableProperty]
    private bool _isHidden;

    /// <summary>Mirror of <see cref="ElementNode.Locked"/>; drives the lock toggle.</summary>
    [ObservableProperty]
    private bool _isLocked;

    /// <summary>True while the name is being edited inline.</summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>The working text shown in the rename editor.</summary>
    [ObservableProperty]
    private string _editText = string.Empty;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            _owner.OnNodeSelected(this);
        }
    }

    /// <summary>Enters inline-rename mode (no-op on locked layers).</summary>
    [RelayCommand]
    private void BeginRename()
    {
        if (IsLocked)
        {
            return;
        }

        EditText = Element.Name ?? string.Empty;
        IsEditing = true;
    }

    /// <summary>Commits the inline rename to the model via the editor.</summary>
    public void CommitRename()
    {
        if (!IsEditing)
        {
            return;
        }

        IsEditing = false;
        _owner.ApplyRename(this, EditText);
    }

    /// <summary>Abandons the inline rename, leaving the name unchanged.</summary>
    public void CancelRename() => IsEditing = false;

    /// <summary>Raises a change notification so the displayed name refreshes.</summary>
    public void RefreshDisplayName() => OnPropertyChanged(nameof(DisplayName));

    [RelayCommand]
    private void Group() => _owner.GroupSelection();

    [RelayCommand]
    private void Ungroup() => _owner.Ungroup(this);

    [RelayCommand]
    private void Delete() => _owner.DeleteNode(this);
}

/// <summary>
/// The functional Layers panel (Phase 8). Two-way selection sync with the canvas,
/// collapse/expand, hide/lock toggles (8a), inline rename + drag-reorder/reparent
/// (8b) and group/ungroup (8c). All edits flow through the editor over the bridge,
/// keeping the Project JSON authoritative.
/// </summary>
public sealed partial class LayersViewModel : ToolViewModel
{
    private readonly IProjectService _projects;
    private readonly EditorSession _editor;
    private readonly Dispatcher _uiDispatcher = Dispatcher.CurrentDispatcher;

    // True while we are mutating IsSelected/IsExpanded from code (canvas echo or a
    // reload), so the two-way bindings don't push the change straight back.
    private bool _suppressPush;

    public LayersViewModel(IProjectService projects, EditorSession editor)
        : base(PanelIds.Layers, "Layers")
    {
        _projects = projects;
        _editor = editor;

        _projects.CurrentChanged += (_, project) => OnUi(() => Load(project));
        _projects.Mutated += (_, project) => OnUi(() => Load(project));
        _editor.SelectionChanged += (_, node) => OnUi(() => SyncSelectionFromCanvas(node?.Id));

        if (_projects.Current is not null)
        {
            Load(_projects.Current);
        }
    }

    public ObservableCollection<LayerNode> Roots { get; } = new();

    /// <summary>Re-reads the current project (e.g. after starter content is applied).</summary>
    public void Reload()
    {
        if (_projects.Current is not null)
        {
            Load(_projects.Current);
        }
    }

    /// <summary>A layer was chosen in the tree → mirror the selection onto the canvas.</summary>
    public void OnNodeSelected(LayerNode node)
    {
        if (_suppressPush)
        {
            return;
        }

        _editor.SelectElement(node.Id);
    }

    /// <summary>Toggles the hidden flag of a layer and pushes it to the editor.</summary>
    [RelayCommand]
    private void ToggleHidden(LayerNode? node)
    {
        if (node is null)
        {
            return;
        }

        node.IsHidden = !node.IsHidden;
        _editor.SetHidden(node.Id, node.IsHidden);
    }

    /// <summary>Toggles the locked flag of a layer and pushes it to the editor.</summary>
    [RelayCommand]
    private void ToggleLocked(LayerNode? node)
    {
        if (node is null)
        {
            return;
        }

        node.IsLocked = !node.IsLocked;
        _editor.SetLocked(node.Id, node.IsLocked);
    }

    /// <summary>Pushes a committed inline rename to the model.</summary>
    public void ApplyRename(LayerNode node, string text)
    {
        var trimmed = text.Trim();
        node.Element.Name = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        node.RefreshDisplayName();
        _editor.Rename(node.Id, trimmed);
    }

    /// <summary>Groups the current canvas selection into a new container.</summary>
    public void GroupSelection() => _editor.GroupSelection();

    /// <summary>Ungroups the given container, lifting its children to its parent.</summary>
    public void Ungroup(LayerNode node) => _editor.Ungroup(node.Id);

    /// <summary>Selects then deletes a layer (locked layers are rejected by the editor).</summary>
    public void DeleteNode(LayerNode node)
    {
        _editor.SelectElement(node.Id);
        _editor.DeleteSelected();
    }

    /// <summary>Finds the parent <see cref="LayerNode"/> of a node, or null for a root.</summary>
    public LayerNode? FindParentNode(LayerNode target)
    {
        LayerNode? Walk(LayerNode node)
        {
            foreach (var child in node.Children)
            {
                if (ReferenceEquals(child, target))
                {
                    return node;
                }

                var found = Walk(child);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        foreach (var root in Roots)
        {
            var found = Walk(root);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Performs a drag-drop move of <paramref name="drag"/> relative to <paramref name="target"/>.</summary>
    public void MoveNode(LayerNode drag, LayerNode target, LayerDropPosition position)
    {
        if (ReferenceEquals(drag, target) || drag.IsLocked)
        {
            return;
        }

        // Never drop a node into itself or one of its own descendants.
        if (IsSelfOrDescendant(drag, target))
        {
            return;
        }

        string parentId;
        int index;

        if (position == LayerDropPosition.Inside)
        {
            parentId = target.Id;
            index = target.Children.Count;
        }
        else
        {
            var parent = FindParentNode(target);
            if (parent is null)
            {
                return; // can't place a sibling next to a page root
            }

            parentId = parent.Id;
            index = parent.Children.IndexOf(target);
            if (position == LayerDropPosition.After)
            {
                index += 1;
            }
        }

        _editor.ReorderElement(drag.Id, parentId, index);
    }

    private static bool IsSelfOrDescendant(LayerNode node, LayerNode candidate)
    {
        if (ReferenceEquals(node, candidate))
        {
            return true;
        }

        return node.Children.Any(child => IsSelfOrDescendant(child, candidate));
    }

    /// <summary>Reflects a canvas selection in the tree without echoing it back.</summary>
    private void SyncSelectionFromCanvas(string? selectedId)
    {
        _suppressPush = true;
        try
        {
            foreach (var node in Roots.SelectMany(Flatten))
            {
                var match = node.Id == selectedId;
                node.IsSelected = match;
                if (match)
                {
                    ExpandAncestorsOf(selectedId!);
                }
            }
        }
        finally
        {
            _suppressPush = false;
        }
    }

    /// <summary>Expands every ancestor of the given node so it is visible in the tree.</summary>
    private void ExpandAncestorsOf(string id)
    {
        bool Walk(LayerNode node)
        {
            if (node.Id == id)
            {
                return true;
            }

            foreach (var child in node.Children)
            {
                if (Walk(child))
                {
                    node.IsExpanded = true;
                    return true;
                }
            }

            return false;
        }

        foreach (var root in Roots)
        {
            if (Walk(root))
            {
                break;
            }
        }
    }

    private void Load(Project project)
    {
        // Preserve in-session UI state across the rebuild (mutations reload the tree).
        var collapsed = Roots.SelectMany(Flatten).Where(n => !n.IsExpanded).Select(n => n.Id).ToHashSet();
        var selectedId = Roots.SelectMany(Flatten).FirstOrDefault(n => n.IsSelected)?.Id;

        _suppressPush = true;
        try
        {
            Roots.Clear();
            var page = project.Pages.FirstOrDefault();
            if (page is not null)
            {
                Roots.Add(new LayerNode(page.Root, this));
            }

            foreach (var node in Roots.SelectMany(Flatten))
            {
                if (collapsed.Contains(node.Id))
                {
                    node.IsExpanded = false;
                }

                node.IsSelected = node.Id == selectedId;
            }
        }
        finally
        {
            _suppressPush = false;
        }
    }

    private static IEnumerable<LayerNode> Flatten(LayerNode node)
    {
        yield return node;
        foreach (var descendant in node.Children.SelectMany(Flatten))
        {
            yield return descendant;
        }
    }

    private void OnUi(Action action)
    {
        if (_uiDispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _uiDispatcher.Invoke(action);
        }
    }
}
