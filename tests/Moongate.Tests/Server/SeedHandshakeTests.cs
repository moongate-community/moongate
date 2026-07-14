using Moongate.Server.Interfaces.Network;
using Moongate.Server.Services.Network;
using Moongate.Server.Types;

namespace Moongate.Tests.Server;

public class SeedHandshakeTests
{
    private sealed class FakeSeedTarget : ISeedTarget
    {
        public SessionStateType State { get; private set; } = SessionStateType.AwaitingSeed;
        public uint? Seed { get; private set; }

        public void SetSeed(uint seed)
            => Seed = seed;

        public void SetState(SessionStateType state)
            => State = state;
    }

    [Fact]
    public void Process_AlreadyPastAwaitingSeed_PassesThrough()
    {
        var target = new FakeSeedTarget();
        target.SetState(SessionStateType.Login);

        var result = SeedHandshake.Process(target, [0x80, 1, 2], out var consumed);

        Assert.Equal(SeedHandshakeResultType.PassThrough, result);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void Process_LoginSeedPacket_MovesToLoginWithoutConsuming()
    {
        var target = new FakeSeedTarget();

        var result = SeedHandshake.Process(target, [0xEF, 0, 0, 0, 0], out var consumed);

        Assert.Equal(SeedHandshakeResultType.PassThrough, result);
        Assert.Equal(0, consumed);
        Assert.Equal(SessionStateType.Login, target.State);
    }

    [Fact]
    public void Process_RawSeed_CapturesSeedAndConsumesFour()
    {
        var target = new FakeSeedTarget();

        var result = SeedHandshake.Process(target, [0x00, 0x00, 0x00, 0x2A, 0x91], out var consumed);

        Assert.Equal(SeedHandshakeResultType.Consumed, result);
        Assert.Equal(4, consumed);
        Assert.Equal(42u, target.Seed);
        Assert.Equal(SessionStateType.Login, target.State);
    }

    [Fact]
    public void Process_ZeroSeed_IsRejected()
    {
        var target = new FakeSeedTarget();

        var result = SeedHandshake.Process(target, [0x00, 0x00, 0x00, 0x00], out _);

        Assert.Equal(SeedHandshakeResultType.Reject, result);
    }
}
