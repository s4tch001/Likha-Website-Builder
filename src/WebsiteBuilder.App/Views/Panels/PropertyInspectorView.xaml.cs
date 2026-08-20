using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WebsiteBuilder.App.ViewModels.Panels;
using WinFormsColorDialog = System.Windows.Forms.ColorDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;

namespace WebsiteBuilder.App.Views.Panels;

public partial class PropertyInspectorView : UserControl
{
    public PropertyInspectorView() => InitializeComponent();

    /// <summary>
    /// Opens the native colour picker for a swatch. The swatch's Tag selects which
    /// colour it edits: "text", "background" or "border".
    /// </summary>
    private void OnPickColor(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PropertyInspectorViewModel viewModel || sender is not Button button)
        {
            return;
        }

        var target = button.Tag as string ?? "text";
        var currentValue = target switch
        {
            "background" => viewModel.Background,
            "border" => viewModel.BorderColor,
            _ => viewModel.TextColor,
        };

        using var dialog = new WinFormsColorDialog { FullOpen = true, AnyColor = true };
        try
        {
            var current = (Color)ColorConverter.ConvertFromString(currentValue);
            dialog.Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B);
        }
        catch (FormatException)
        {
            // No valid current colour; start from the dialog default.
        }

        if (dialog.ShowDialog() != WinFormsDialogResult.OK)
        {
            return;
        }

        var c = dialog.Color;
        var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        switch (target)
        {
            case "background":
                viewModel.Background = hex;
                break;
            case "border":
                viewModel.BorderColor = hex;
                break;
            default:
                viewModel.TextColor = hex;
                break;
        }
    }
}
