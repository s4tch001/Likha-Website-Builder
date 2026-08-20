using System.Collections.ObjectModel;

namespace WebsiteBuilder.App.Commands;

/// <summary>
/// Central catalogue of every <see cref="AppCommand"/> in the app. The ribbon and
/// the command palette are both projections of this registry, guaranteeing they
/// stay in sync. Registered as a singleton so any phase can contribute commands.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>All registered commands, in registration order.</summary>
    ReadOnlyObservableCollection<AppCommand> Commands { get; }

    /// <summary>Adds a command. Throws if the id is already registered.</summary>
    void Register(AppCommand command);

    /// <summary>Looks up a command by id, returning null if not found.</summary>
    AppCommand? Find(string id);

    /// <summary>Looks up a command by id; throws if missing. Used by XAML/code that requires it.</summary>
    AppCommand Get(string id);

    /// <summary>Indexer form of <see cref="Get"/> so XAML can bind: Registry[file.new].</summary>
    AppCommand this[string id] { get; }
}
