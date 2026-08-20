using System.IO;
using WebsiteBuilder.CodeGen;
using WebsiteBuilder.Core.Services;

namespace WebsiteBuilder.App.Services;

public sealed class PreviewOptions
{
    public string RootDirectory { get; init; } = Path.Combine(
        Path.GetTempPath(),
        "LikhaWebsiteBuilder",
        "Preview");
}

/// <summary>Owns the generated snapshot, loopback server, browser launch, and cleanup lifecycle.</summary>
public sealed class PreviewService(
    IProjectService projects,
    ProjectExportService exporter,
    IPreviewBrowser browser,
    PreviewOptions options) : IDisposable, IAsyncDisposable
{
    private const string RunPrefix = "PreviewRun-";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LocalPreviewServer? _server;
    private string? _runDirectory;

    public event EventHandler? StateChanged;

    public bool IsRunning => _server is not null;

    public Uri? Url => _server?.Url;

    public string? RunDirectory => _runDirectory;

    public async Task<Uri> StartAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_server?.Url is { } existing)
            {
                return existing;
            }

            var project = projects.Current
                ?? throw new InvalidOperationException("There is no project to preview.");
            var previewRoot = Path.GetFullPath(options.RootDirectory);
            Directory.CreateDirectory(previewRoot);
            var runDirectory = Path.Combine(previewRoot, RunPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runDirectory);

            var server = new LocalPreviewServer();
            try
            {
                await exporter.ExportAsync(
                    new HtmlCodeGenerator(),
                    project,
                    runDirectory,
                    projects.ProjectDirectory,
                    cancellationToken).ConfigureAwait(false);
                await server.StartAsync(runDirectory, cancellationToken).ConfigureAwait(false);
                var url = server.Url ?? throw new InvalidOperationException("Preview server did not provide a URL.");
                browser.Open(url);

                _runDirectory = runDirectory;
                _server = server;
                StateChanged?.Invoke(this, EventArgs.Empty);
                return url;
            }
            catch
            {
                await server.DisposeAsync().ConfigureAwait(false);
                DeleteRunDirectory(previewRoot, runDirectory);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var server = _server;
            var runDirectory = _runDirectory;
            _server = null;
            _runDirectory = null;
            if (server is null)
            {
                return;
            }

            await server.DisposeAsync().ConfigureAwait(false);
            if (runDirectory is not null)
            {
                DeleteRunDirectory(Path.GetFullPath(options.RootDirectory), runDirectory);
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void DeleteRunDirectory(string previewRoot, string runDirectory)
    {
        var rootPrefix = previewRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullRunDirectory = Path.GetFullPath(runDirectory);
        if (!fullRunDirectory.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(fullRunDirectory).StartsWith(RunPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to clean an unexpected preview directory.");
        }

        if (Directory.Exists(fullRunDirectory))
        {
            Directory.Delete(fullRunDirectory, recursive: true);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _gate.Dispose();
    }
}
