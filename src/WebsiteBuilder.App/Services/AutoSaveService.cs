using System.IO;
using System.Windows.Threading;
using WebsiteBuilder.Core.Serialization;
using WebsiteBuilder.Core.Services;

namespace WebsiteBuilder.App.Services;

/// <summary>
/// Debounced auto-save: a short while after the project is mutated it is written
/// back to its file. Projects that have never been saved (no path yet) get a
/// recovery snapshot in LocalAppData instead, so unsaved work survives a crash.
/// A successful save to a real location clears that recovery copy.
/// </summary>
public sealed class AutoSaveService
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromSeconds(2);

    private readonly IProjectService _projects;
    private readonly DispatcherTimer _timer;
    private readonly string _recoveryPath;

    public AutoSaveService(IProjectService projects)
    {
        _projects = projects;
        _recoveryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WebsiteBuilder", "recovery", "autosave.json");

        _timer = new DispatcherTimer { Interval = DebounceInterval };
        _timer.Tick += OnTick;

        // Each edit (re)starts the debounce window; the save runs once it settles.
        _projects.Mutated += (_, _) => Schedule();
    }

    /// <summary>Raised after an auto-save attempt with a short human-readable result.</summary>
    public event EventHandler<string>? AutoSaved;

    /// <summary>True if a recovery snapshot from a previous session exists on disk.</summary>
    public bool HasRecovery => File.Exists(_recoveryPath);

    private void Schedule()
    {
        _timer.Stop();
        _timer.Start();
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        await RunAsync().ConfigureAwait(true);
    }

    private async Task RunAsync()
    {
        if (!_projects.IsDirty || _projects.Current is null)
        {
            return;
        }

        try
        {
            if (_projects.CurrentPath is not null)
            {
                await _projects.SaveAsync().ConfigureAwait(true);
                DeleteRecovery();
                AutoSaved?.Invoke(this, $"Auto-saved {DateTime.Now:HH:mm:ss}");
            }
            else
            {
                WriteRecovery();
                AutoSaved?.Invoke(this, $"Recovery copy saved {DateTime.Now:HH:mm:ss} (use Save As to keep it)");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AutoSaved?.Invoke(this, $"Auto-save failed: {ex.Message}");
        }
    }

    private void WriteRecovery()
    {
        var directory = Path.GetDirectoryName(_recoveryPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(_recoveryPath, ProjectSerializer.Serialize(_projects.Current!));
    }

    private void DeleteRecovery()
    {
        try
        {
            if (File.Exists(_recoveryPath))
            {
                File.Delete(_recoveryPath);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; a stale recovery file is harmless.
        }
    }
}
