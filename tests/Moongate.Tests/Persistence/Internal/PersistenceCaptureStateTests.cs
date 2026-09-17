using Moongate.Persistence.Internal;

namespace Moongate.Tests.Persistence.Internal;

public sealed class PersistenceCaptureStateTests
{
    [Fact]
    public void DuplicateBeforeClose_InvalidatesCapturedState()
    {
        var state = new PersistenceCaptureState();
        state.BeginCapture();
        state.CompleteCapture();

        Assert.Throws<InvalidOperationException>(state.BeginCapture);

        Assert.False(state.Close());
    }

    [Fact]
    public void DuplicateAfterClose_DoesNotInvalidateAcceptedCapture()
    {
        var state = new PersistenceCaptureState();
        state.BeginCapture();
        state.CompleteCapture();

        Assert.True(state.Close());
        Assert.Throws<InvalidOperationException>(state.BeginCapture);
    }
}
