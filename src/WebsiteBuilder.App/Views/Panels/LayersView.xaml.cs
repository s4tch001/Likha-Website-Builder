using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WebsiteBuilder.App.ViewModels.Panels;

namespace WebsiteBuilder.App.Views.Panels;

public partial class LayersView : UserControl
{
    private const string LayerDataFormat = "WebsiteBuilder.LayerNode";

    private Point _pressPoint;
    private LayerNode? _pressNode;
    private LayerDropAdorner? _adorner;
    private TreeViewItem? _adornedItem;

    public LayersView() => InitializeComponent();

    // --- Inline rename ---------------------------------------------------------

    private void OnRenameBoxVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // When the rename editor appears, focus it and select all text.
        if (sender is TextBox box && box.IsVisible)
        {
            box.Dispatcher.BeginInvoke(() =>
            {
                box.Focus();
                box.SelectAll();
            });
        }
    }

    private void OnRenameBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: LayerNode node })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            node.CommitRename();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            node.CancelRename();
            e.Handled = true;
        }
    }

    private void OnRenameBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: LayerNode node })
        {
            node.CommitRename();
        }
    }

    // --- Drag-reorder ----------------------------------------------------------

    private void OnTreePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Ignore presses that begin on the row's buttons or the rename editor.
        if (FindAncestor<ButtonBase>((DependencyObject)e.OriginalSource) is not null
            || FindAncestor<TextBox>((DependencyObject)e.OriginalSource) is not null)
        {
            _pressNode = null;
            return;
        }

        _pressPoint = e.GetPosition(LayersTree);
        _pressNode = NodeAt(e.OriginalSource);
    }

    private void OnTreePreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pressNode is null)
        {
            return;
        }

        var p = e.GetPosition(LayersTree);
        if (Math.Abs(p.X - _pressPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(p.Y - _pressPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        // The page root and locked layers cannot be dragged.
        if (_pressNode.IsLocked || (DataContext is LayersViewModel vm && vm.FindParentNode(_pressNode) is null))
        {
            _pressNode = null;
            return;
        }

        var node = _pressNode;
        _pressNode = null;
        DragDrop.DoDragDrop(LayersTree, new DataObject(LayerDataFormat, node), DragDropEffects.Move);
        ClearAdorner();
    }

    private void OnTreeDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(LayerDataFormat) || DataContext is not LayersViewModel)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var item = ItemAt(e.OriginalSource);
        if (item?.DataContext is not LayerNode target)
        {
            ClearAdorner();
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        ShowAdorner(item, DropPositionFor(item, e.GetPosition(item)));
        e.Handled = true;
    }

    private void OnTreeDragLeave(object sender, DragEventArgs e) => ClearAdorner();

    private void OnTreeDrop(object sender, DragEventArgs e)
    {
        ClearAdorner();
        if (DataContext is not LayersViewModel vm
            || !e.Data.GetDataPresent(LayerDataFormat)
            || e.Data.GetData(LayerDataFormat) is not LayerNode drag)
        {
            return;
        }

        var item = ItemAt(e.OriginalSource);
        if (item?.DataContext is not LayerNode target)
        {
            return;
        }

        vm.MoveNode(drag, target, DropPositionFor(item, e.GetPosition(item)));
        e.Handled = true;
    }

    private static LayerDropPosition DropPositionFor(TreeViewItem item, Point pos)
    {
        // Use the header band (top of the item) split into thirds.
        var h = Math.Min(item.ActualHeight, 22);
        if (pos.Y < h * 0.33)
        {
            return LayerDropPosition.Before;
        }

        return pos.Y > h * 0.66 ? LayerDropPosition.After : LayerDropPosition.Inside;
    }

    private void ShowAdorner(TreeViewItem item, LayerDropPosition position)
    {
        if (!ReferenceEquals(_adornedItem, item))
        {
            ClearAdorner();
            var layer = AdornerLayer.GetAdornerLayer(item);
            if (layer is null)
            {
                return;
            }

            _adorner = new LayerDropAdorner(item);
            layer.Add(_adorner);
            _adornedItem = item;
        }

        _adorner?.Update(position);
    }

    private void ClearAdorner()
    {
        if (_adorner is not null && _adornedItem is not null)
        {
            AdornerLayer.GetAdornerLayer(_adornedItem)?.Remove(_adorner);
        }

        _adorner = null;
        _adornedItem = null;
    }

    // --- Hit-testing helpers ---------------------------------------------------

    private LayerNode? NodeAt(object source) => ItemAt(source)?.DataContext as LayerNode;

    private static TreeViewItem? ItemAt(object source) =>
        source is DependencyObject dep ? FindAncestor<TreeViewItem>(dep) : null;

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
