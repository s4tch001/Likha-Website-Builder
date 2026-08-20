using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WebsiteBuilder.App.Models;
using WebsiteBuilder.Core.Serialization;

namespace WebsiteBuilder.App.Views.Panels;

public partial class ComponentsView : UserControl
{
    public ComponentsView() => InitializeComponent();

    private const string ComponentMime = "application/x-wb-component";
    private const string ComponentTextPrefix = "likha-component:";
    private Point _dragStart;
    private ComponentItem? _dragItem;

    private void Component_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragItem = (sender as FrameworkElement)?.DataContext as ComponentItem;
    }

    private void Component_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem?.Definition is not { } definition)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (Math.Abs(point.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(point.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var json = JsonSerializer.Serialize(new
        {
            componentId = definition.Id,
            root = definition.Root,
        }, ProjectSerializer.Options);
        var data = new DataObject();
        data.SetData(ComponentMime, json);
        data.SetData(DataFormats.Text, ComponentTextPrefix + json);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy);
        _dragItem = null;
    }
}
