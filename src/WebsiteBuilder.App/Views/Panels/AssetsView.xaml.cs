using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WebsiteBuilder.App.ViewModels.Panels;

namespace WebsiteBuilder.App.Views.Panels;

public partial class AssetsView : UserControl
{
    public AssetsView() => InitializeComponent();

    private Point _dragStart;

    private void AssetList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _dragStart = e.GetPosition(this);

    private void AssetList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || DataContext is not AssetsViewModel { SelectedAsset: { } item })
        {
            return;
        }

        var point = e.GetPosition(this);
        if (Math.Abs(point.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(point.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var data = new DataObject();
        data.SetData("application/x-wb-asset-id", item.Asset.Id);
        data.SetData(DataFormats.FileDrop, new[] { item.FullPath });
        DragDrop.DoDragDrop(AssetList, data, DragDropEffects.Copy);
    }

    private void AssetList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AssetsViewModel { SelectedAsset: { } item } viewModel)
        {
            viewModel.UseCommand.Execute(item);
        }
    }
}
