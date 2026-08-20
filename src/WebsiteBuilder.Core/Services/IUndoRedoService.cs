namespace WebsiteBuilder.Core.Services;

/// <summary>
/// A reversible operation. Mutations to the project are expressed as commands so
/// they can be pushed onto the history stack and undone/redone deterministically.
/// The concrete command set and integration land in Phase 15.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>Short, user-facing label (e.g. "Move Button").</summary>
    string Label { get; }

    /// <summary>Applies the change.</summary>
    void Execute();

    /// <summary>Reverts the change applied by <see cref="Execute"/>.</summary>
    void Undo();
}

/// <summary>
/// Unlimited undo/redo history. Implementation arrives in Phase 15; the contract
/// is fixed now so editor mutations can be funneled through it from the start.
/// </summary>
public interface IUndoRedoService
{
    bool CanUndo { get; }
    bool CanRedo { get; }

    /// <summary>Raised after any push/undo/redo so the UI can refresh command state.</summary>
    event EventHandler? StateChanged;

    /// <summary>Executes a command and records it on the undo stack (clearing the redo stack).</summary>
    void Execute(IUndoableCommand command);

    /// <summary>Reverts the most recent command.</summary>
    void Undo();

    /// <summary>Re-applies the most recently undone command.</summary>
    void Redo();

    /// <summary>Discards all recorded history.</summary>
    void Clear();
}
