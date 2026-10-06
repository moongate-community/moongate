using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.General;

public sealed class ExtendedCommandStatLockTests : IAsyncLifetime
{
    private readonly RecordingMobileStateService _state = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private ExtendedCommandPacketHandler _handler = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _handler = new(
            TestTooltips.Create(TestItems.Create(_fixture.Sectors), _fixture.Mobiles),
            _fixture.Sender,
            _fixture.Mobiles,
            _state
        );
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Theory]
    [InlineData(0, 0, StatType.Str, StatLockType.Up)]
    [InlineData(0, 1, StatType.Str, StatLockType.Down)]
    [InlineData(1, 2, StatType.Dex, StatLockType.Locked)]
    [InlineData(2, 1, StatType.Int, StatLockType.Down)]
    public void Subcommand0x1A_SetsTheLockOfTheStatOfTheCharacter(
        byte stat, byte lockValue, StatType expectedStat, StatLockType expected
    )
    {
        Handle(stat, lockValue);

        Assert.Equal([(_aria, expectedStat, expected)], _state.StatLocksSet);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(255)]
    public void Subcommand0x1A_ALockThatDoesNotExist_IsReadAsUp_AsModernUO(byte lockValue)
    {
        Handle(1, lockValue);

        Assert.Equal([(_aria, StatType.Dex, StatLockType.Up)], _state.StatLocksSet);
    }

    [Fact]
    public void Subcommand0x1A_WithoutItsTwoBytes_ChangesNothing()
    {
        Assert.True(ExtendedCommandPacket.TryParse(Convert.FromHexString("BF0006001A00"), out var packet));

        _handler.Handle(_session, packet);

        Assert.Empty(_state.StatLocksSet);
    }

    [Fact]
    public async Task Subcommand0x1A_OfASessionWithoutACharacterInTheWorld_ChangesNothing()
    {
        var lobby = await _fixture.AddAsync(3, false);
        Assert.True(ExtendedCommandPacket.TryParse(Convert.FromHexString("BF0007001A0001"), out var packet));

        _handler.Handle(lobby, packet);

        Assert.Empty(_state.StatLocksSet);
    }

    private void Handle(byte stat, byte lockValue)
    {
        Assert.True(
            ExtendedCommandPacket.TryParse(Convert.FromHexString($"BF0007001A{stat:X2}{lockValue:X2}"), out var packet)
        );

        _handler.Handle(_session, packet);
    }
}
