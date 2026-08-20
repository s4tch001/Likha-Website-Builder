using WebsiteBuilder.Core.Services;
using Xunit;

namespace WebsiteBuilder.Core.Tests;

public class UndoRedoServiceTests
{
    private sealed class DelegateCommand : IUndoableCommand
    {
        private readonly Action _do;
        private readonly Action _undo;

        public DelegateCommand(string label, Action execute, Action undo)
        {
            Label = label;
            _do = execute;
            _undo = undo;
        }

        public string Label { get; }
        public void Execute() => _do();
        public void Undo() => _undo();
    }

    [Fact]
    public void Execute_Undo_Redo_RunsCommandInOrder()
    {
        var service = new UndoRedoService();
        var value = 0;
        var command = new DelegateCommand("inc", () => value++, () => value--);

        Assert.False(service.CanUndo);

        service.Execute(command);
        Assert.Equal(1, value);
        Assert.True(service.CanUndo);
        Assert.False(service.CanRedo);

        service.Undo();
        Assert.Equal(0, value);
        Assert.False(service.CanUndo);
        Assert.True(service.CanRedo);

        service.Redo();
        Assert.Equal(1, value);
        Assert.True(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Execute_ClearsRedoStack()
    {
        var service = new UndoRedoService();
        var command = new DelegateCommand("noop", () => { }, () => { });

        service.Execute(command);
        service.Undo();
        Assert.True(service.CanRedo);

        service.Execute(command);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void StateChanged_RaisedOnMutations()
    {
        var service = new UndoRedoService();
        var raised = 0;
        service.StateChanged += (_, _) => raised++;

        var command = new DelegateCommand("noop", () => { }, () => { });
        service.Execute(command);
        service.Undo();
        service.Redo();

        Assert.Equal(3, raised);
    }
}
