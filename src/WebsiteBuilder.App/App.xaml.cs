using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebsiteBuilder.App.Commands;
using WebsiteBuilder.App.Services;
using WebsiteBuilder.App.ViewModels;
using WebsiteBuilder.App.ViewModels.Panels;
using WebsiteBuilder.App.Views;
using WebsiteBuilder.Core.Services;

namespace WebsiteBuilder.App;

/// <summary>
/// Application entry point. Builds a Microsoft.Extensions generic host so the
/// whole app shares one DI container, configuration and logging pipeline, then
/// resolves and shows the main window. Services for each subsequent phase are
/// registered in <see cref="ConfigureServices"/>.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    /// <summary>
    /// Process-wide service provider. Views that are instantiated by XAML (and so
    /// cannot use constructor injection) resolve infrastructure services through
    /// this accessor.
    /// </summary>
    public static IServiceProvider Services =>
        ((App)Current)._host.Services;

    /// <summary>Stable taskbar identity so Windows uses the window icon (not a cached shell icon).</summary>
    private const string AppUserModelId = "Likha.WebsiteBuilder";

    [DllImport("shell32.dll", PreserveSig = false)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appId);

    public App()
    {
        // Give the app an explicit taskbar identity. Without this, Windows can fall
        // back to a cached/generic shell icon for the executable on the taskbar even
        // though the window icon is set correctly.
        try
        {
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        }
        catch (COMException)
        {
            // Non-fatal; the window icon still applies.
        }

        // Opt-in data-binding diagnostics: set WB_TRACE_BINDINGS=1 to surface WPF
        // binding failures on the console. Used during development/verification.
        var bindingLog = Environment.GetEnvironmentVariable("WB_TRACE_BINDINGS");
        if (!string.IsNullOrEmpty(bindingLog))
        {
            PresentationTraceSources.Refresh();
            var listener = new TextWriterTraceListener(bindingLog) { TraceOutputOptions = TraceOptions.None };
            listener.WriteLine("[binding-trace started]");
            listener.Flush();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
            Trace.AutoFlush = true;
        }

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) => ConfigureServices(services))
            .Build();
    }

    /// <summary>Composition root: register view models, views and services here.</summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IUndoRedoService, UndoRedoService>();
        services.AddSingleton(new AssetImportOptions());
        services.AddSingleton<IAssetService, AssetService>();

        // App services
        services.AddSingleton<ICommandRegistry, CommandRegistry>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<EditorSession>();
        services.AddSingleton<AutoSaveService>();

        // Panel / document view models
        services.AddSingleton<ProjectExplorerViewModel>();
        services.AddSingleton<LayersViewModel>();
        services.AddSingleton<ComponentsViewModel>();
        services.AddSingleton<AssetsViewModel>();
        services.AddSingleton<PropertyInspectorViewModel>();
        services.AddSingleton<FileManagerViewModel>();
        services.AddSingleton<CanvasViewModel>();
        services.AddSingleton<CommandPaletteViewModel>();

        // Shell
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        await _host.StartAsync().ConfigureAwait(true);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync().ConfigureAwait(true);
        _host.Dispose();

        base.OnExit(e);
    }
}
