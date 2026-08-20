namespace WebsiteBuilder.Core.Services;

/// <summary>
/// Unlimited undo/redo via two command stacks. Executing a new command clears
/// the redo stack, matching the standard linear-history model. Editor mutations
/// are funneled through <see cref="Execute"/> as commands starting in Phase 15;
/// the mechanism itself is complete and tested now.
/// </summary>
public sealed class UndoRedoService : IUndoRedoService
{
    private readonly Stack<IUndoableCommand> _undo = new();
    private readonly Stack<IUndoableCommand> _redo = new();

    /// <inheritdoc />
    public bool CanUndo => _undo.Count > 0;

    /// <inheritdoc />
    public bool CanRedo => _redo.Count > 0;

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public void Execute(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Execute();
        _undo.Push(command);
        _redo.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        var command = _undo.Pop();
        command.Undo();
        _redo.Push(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        var command = _redo.Pop();
        command.Execute();
        _undo.Push(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Clear()
    {
        if (_undo.Count == 0 && _redo.Count == 0)
        {
            return;
        }

        _undo.Clear();
        _redo.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
