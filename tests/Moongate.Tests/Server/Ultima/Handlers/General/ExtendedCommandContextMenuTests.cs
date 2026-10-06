using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.ContextMenus;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Tooltips;

namespace Moongate.Tests.Server.Ultima.Handlers.General;

/// <summary>
///     The two extended commands of a context menu, from their bytes to the service.
/// </summary>
public sealed class ExtendedCommandContextMenuTests : IAsyncLifetime
{
    private readonly RecordingContextMenuService _menus = new();
    private readonly StubPacketSendService _sender = new();

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;
    private ExtendedCommandPacketHandler _handler = null!;

    public async Task InitializeAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.CharacterId, new Serial(2)));
        var sectors = TestSectors.Create();
        var mobiles = new MobileService(new StubMovementService(), sectors);
        _handler = new(TestTooltips.Create(TestItems.Create(sectors), mobiles), _sender, mobiles, contextMenus: _menus);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    // Sub-command 0x13: the serial of what the player clicked.
    [Fact]
    public async Task ARequest_AsksForTheMenuOfTheTarget()
    {
        await HandleAsync("BF" + "0009" + "0013" + "00000100");

        Assert.Equal((_session, new Serial(0x100)), Assert.Single(_menus.Requested));
        Assert.Empty(_menus.Selected);
    }

    // Sub-command 0x15: the serial again and the index of the entry chosen.
    [Fact]
    public async Task AChoice_ChoosesTheEntryOfThatIndex()
    {
        await HandleAsync("BF" + "000B" + "0015" + "00000100" + "0002");

        Assert.Equal((_session, new Serial(0x100), 2), Assert.Single(_menus.Selected));
        Assert.Empty(_menus.Requested);
    }

    [Theory]
    [InlineData("BF" + "0007" + "0013" + "0000")]
    [InlineData("BF" + "0009" + "0015" + "00000100")]
    [InlineData("BF" + "0005" + "0013")]
    public async Task AShortPacket_IsIgnored(string hex)
    {
        await HandleAsync(hex);

        Assert.Empty(_menus.Requested);
        Assert.Empty(_menus.Selected);
    }

    private Task HandleAsync(string hex)
    {
        Assert.True(ExtendedCommandPacket.TryParse(Convert.FromHexString(hex), out var packet));

        return _fixture.ExecuteOnLoopAsync(() => _handler.Handle(_session, packet));
    }
}
