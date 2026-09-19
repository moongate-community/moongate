using Moongate.Scripting.Internal;

namespace Moongate.Tests.Scripting.Internal;

public sealed class SyncValueTaskTests
{
    [Fact]
    public void Run_CompletedTask_ReturnsItsResult()
    {
        Assert.Equal(7, SyncValueTask.Run(new ValueTask<int>(7)));
    }

    [Fact]
    public void Run_FaultedTask_RethrowsTheOriginalException()
    {
        var task = new ValueTask<int>(Task.FromException<int>(new InvalidDataException("boom")));

        var exception = Assert.Throws<InvalidDataException>(() => SyncValueTask.Run(task));

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public void Run_IncompleteTask_ThrowsInsteadOfBlocking()
    {
        var source = new TaskCompletionSource<int>();

        var exception = Assert.Throws<InvalidOperationException>(() => SyncValueTask.Run(new ValueTask<int>(source.Task)));

        Assert.Contains("asynchronous", exception.Message, StringComparison.Ordinal);
        source.SetResult(0);
    }

    [Fact]
    public void Run_NonGenericCompleted_Passes()
    {
        SyncValueTask.Run(ValueTask.CompletedTask);
    }
}
