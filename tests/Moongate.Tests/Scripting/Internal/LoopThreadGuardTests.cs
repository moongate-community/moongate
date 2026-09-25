using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Internal;

public sealed class LoopThreadGuardTests
{
    [Fact]
    public void EnsureScriptThread_OffTheLoop_ThrowsNamingTheMember()
    {
        var guard = new LoopThreadGuard(new StubGameLoop { IsOnLoopThread = false });

        var exception = Assert.Throws<InvalidOperationException>(() => guard.EnsureScriptThread("LoadFile"));

        Assert.Contains("LoadFile", exception.Message, StringComparison.Ordinal);
        Assert.Contains("game loop thread", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureScriptThread_OnTheLoop_Passes()
    {
        var guard = new LoopThreadGuard(new StubGameLoop { IsOnLoopThread = true });

        guard.EnsureScriptThread("LoadFile");
    }

    [Fact]
    public void NoThreadGuard_AlwaysPasses()
    {
        NoThreadGuard.Instance.EnsureScriptThread("anything");
    }
}
