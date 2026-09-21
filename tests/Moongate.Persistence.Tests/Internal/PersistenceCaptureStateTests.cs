using Moongate.Persistence.Internal;

namespace Moongate.Persistence.Tests.Internal;

public sealed class PersistenceCaptureStateTests
{
    [Fact]
    public async Task DuplicateBeforeClose_InvalidatesCapturedState()
    {
        var state = new PersistenceCaptureState();
        state.BeginCapture();
        state.CompleteCapture();
        state.ExitCapture();

        Assert.Throws<InvalidOperationException>(state.BeginCapture);

        Assert.False(await state.CloseAsync());
    }

    [Fact]
    public async Task DuplicateAfterClose_DoesNotInvalidateAcceptedCapture()
    {
        var state = new PersistenceCaptureState();
        state.BeginCapture();
        state.CompleteCapture();
        state.ExitCapture();

        Assert.True(await state.CloseAsync());
        Assert.Throws<InvalidOperationException>(state.BeginCapture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CloseAsync_AdmittedCaptureHasNotExited_DrainsAndRetainsInvalidResult(bool completed)
    {
        var state = new PersistenceCaptureState();
        state.BeginCapture();
        if (completed)
        {
            state.CompleteCapture();
        }

        var close = state.CloseAsync();
        Assert.False(close.IsCompleted);
        Assert.Throws<InvalidOperationException>(state.BeginCapture);
        Assert.Throws<InvalidOperationException>(state.CompleteCapture);
        state.ExitCapture();
        Assert.False(await close);
    }
}
