using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WebsiteBuilder.App.ViewModels.Panels;

namespace WebsiteBuilder.App.Views.Panels;

/// <summary>
/// Visual feedback for a Layers drag-drop: a horizontal insertion line for
/// <see cref="LayerDropPosition.Before"/>/<see cref="LayerDropPosition.After"/>,
/// or a rounded outline for <see cref="LayerDropPosition.Inside"/>. Drawn over the
/// header area of the target tree item.
/// </summary>
internal sealed class LayerDropAdorner : Adorner
{
    private const double HeaderHeight = 22;
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));

    private readonly Pen _pen;
    private LayerDropPosition _position;

    public LayerDropAdorner(UIElement adornedElement)
        : base(adornedElement)
    {
        IsHitTestVisible = false;
        _pen = new Pen(Accent, 2);
        _pen.Freeze();
    }

    public void Update(LayerDropPosition position)
    {
        _position = position;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = ((FrameworkElement)AdornedElement).ActualWidth;
        var height = Math.Min(((FrameworkElement)AdornedElement).ActualHeight, HeaderHeight);

        switch (_position)
        {
            case LayerDropPosition.Before:
                drawingContext.DrawLine(_pen, new Point(0, 1), new Point(width, 1));
                break;
            case LayerDropPosition.After:
                drawingContext.DrawLine(_pen, new Point(0, height - 1), new Point(width, height - 1));
                break;
            case LayerDropPosition.Inside:
                drawingContext.DrawRoundedRectangle(
                    null, _pen, new Rect(1, 1, Math.Max(0, width - 2), Math.Max(0, height - 2)), 3, 3);
                break;
        }
    }
}
