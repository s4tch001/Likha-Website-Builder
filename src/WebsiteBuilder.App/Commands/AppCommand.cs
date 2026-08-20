using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace WebsiteBuilder.App.Commands;

/// <summary>
/// A single, addressable application command. One <see cref="AppCommand"/> is the
/// single source of truth for an action: the ribbon, the command palette and any
/// keyboard shortcut all bind to the same instance, so behaviour and enablement
/// never diverge (no duplicated command logic).
/// </summary>
public sealed class AppCommand
{
    public AppCommand(
        string id,
        string title,
        string category,
        ICommand command,
        string? gestureText = null,
        string? glyph = null,
        string? description = null)
    {
        Id = id;
        Title = title;
        Category = category;
        Command = command;
        GestureText = gestureText;
        Glyph = glyph;
        Description = description;
    }

    /// <summary>Stable identifier (e.g. "file.new"). Used to look the command up.</summary>
    public string Id { get; }

    /// <summary>User-facing label shown on the ribbon and in the palette.</summary>
    public string Title { get; }

    /// <summary>Grouping used by the ribbon tab/group and palette section (e.g. "File").</summary>
    public string Category { get; }

    /// <summary>The executable command. Drives both invocation and CanExecute-based enablement.</summary>
    public ICommand Command { get; }

    /// <summary>Human-readable shortcut hint (e.g. "Ctrl+S"); null if none.</summary>
    public string? GestureText { get; }

    /// <summary>Optional Segoe MDL2 / text glyph for the ribbon button.</summary>
    public string? Glyph { get; }

    /// <summary>Optional longer description for the command palette.</summary>
    public string? Description { get; }

    /// <summary>Convenience factory for a parameterless command backed by an <see cref="Action"/>.</summary>
    public static AppCommand Create(
        string id,
        string title,
        string category,
        Action execute,
        Func<bool>? canExecute = null,
        string? gestureText = null,
        string? glyph = null,
        string? description = null)
    {
        var relay = canExecute is null ? new RelayCommand(execute) : new RelayCommand(execute, canExecute);
        return new AppCommand(id, title, category, relay, gestureText, glyph, description);
    }
}
