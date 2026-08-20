using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using WebsiteBuilder.App.Services;

namespace WebsiteBuilder.App.Views;

/// <summary>
/// Hosts the WebView2 control that runs the React editor. Because the control is
/// created by XAML, it resolves the singleton <see cref="EditorSession"/> from the
/// application service provider and asks it to bind to the WebView2 instance.
/// </summary>
public partial class EditorHostControl : UserControl
{
    private bool _initialized;

    public EditorHostControl() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        var session = App.Services.GetRequiredService<EditorSession>();
        await session.AttachAsync(WebView);
    }
}
