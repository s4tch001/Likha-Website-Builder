using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteBuilder.App.Commands;

namespace WebsiteBuilder.App.ViewModels;

/// <summary>
/// A VS/Figma-style command palette. It is a pure projection of the shared
/// <see cref="ICommandRegistry"/>: opening it lists every command, typing filters
/// them, and executing runs the exact same <see cref="AppCommand"/> the ribbon
/// uses. Toggled with Ctrl+Shift+P.
/// </summary>
public sealed partial class CommandPaletteViewModel : ObservableObject
{
    private readonly ICommandRegistry _registry;

    public CommandPaletteViewModel(ICommandRegistry registry)
    {
        _registry = registry;
    }

    public ObservableCollection<AppCommand> Results { get; } = new();

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private AppCommand? _selectedCommand;

    partial void OnQueryChanged(string value) => Refresh();

    [RelayCommand]
    public void Open()
    {
        Query = string.Empty;
        Refresh();
        IsOpen = true;
    }

    [RelayCommand]
    public void Close() => IsOpen = false;

    [RelayCommand]
    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    [RelayCommand]
    private void ExecuteSelected()
    {
        var command = SelectedCommand;
        Close();

        if (command is not null && command.Command.CanExecute(null))
        {
            command.Command.Execute(null);
        }
    }

    private void Refresh()
    {
        Results.Clear();

        var query = Query?.Trim();
        IEnumerable<AppCommand> matches = _registry.Commands;

        if (!string.IsNullOrEmpty(query))
        {
            matches = matches.Where(c =>
                c.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var command in matches)
        {
            Results.Add(command);
        }

        SelectedCommand = Results.FirstOrDefault();
    }
}
