using System.Collections.ObjectModel;

namespace WebsiteBuilder.App.Commands;

/// <inheritdoc cref="ICommandRegistry" />
public sealed class CommandRegistry : ICommandRegistry
{
    private readonly ObservableCollection<AppCommand> _commands = new();
    private readonly Dictionary<string, AppCommand> _byId = new(StringComparer.Ordinal);

    public CommandRegistry()
    {
        Commands = new ReadOnlyObservableCollection<AppCommand>(_commands);
    }

    /// <inheritdoc />
    public ReadOnlyObservableCollection<AppCommand> Commands { get; }

    /// <inheritdoc />
    public void Register(AppCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_byId.TryAdd(command.Id, command))
        {
            throw new InvalidOperationException($"A command with id '{command.Id}' is already registered.");
        }

        _commands.Add(command);
    }

    /// <inheritdoc />
    public AppCommand? Find(string id) => _byId.GetValueOrDefault(id);

    /// <inheritdoc />
    public AppCommand Get(string id) =>
        Find(id) ?? throw new KeyNotFoundException($"No command registered with id '{id}'.");

    /// <inheritdoc />
    public AppCommand this[string id] => Get(id);
}
