using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Targeting;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Targeting;

public sealed class TargetResponsePacketHandlerTests : IAsyncDisposable
{
    private readonly StubPacketSendService _sender = new();
    private readonly FakeMapService _map = new(16, 16);
    private readonly FakeTileDataService _tiles = new();
    private readonly StubMovementService _movement = new() { LandingZ = 7 };
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly ItemService _items = TestItems.Create();

    private readonly MobileEntity _aria = new()
    {
        Id = new(2), Name = "Aria", Map = MapType.Felucca, Location = new Point3D(5, 5, 0)
    };

    private readonly List<TargetResult> _results = [];

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;
    private TargetService _targets = null!;

    [Fact]
    public async Task Handle_ALiveItem_GivesTheObject()
    {
        _items.Add([new ItemEntity { Id = new(0x40000010), TemplateId = "gold", ItemId = 0x0EED, Amount = 1 }]);
        await PendingAsync();

        await RespondAsync(1, 0x40000010, 0, 0, 0, 0x0EED);

        var result = Assert.Single(_results);
        Assert.Equal((TargetResultType.Object, new Serial(0x40000010)), (result.Kind, result.Serial));
    }

    [Fact]
    public async Task Handle_ALiveMobile_GivesTheObject()
    {
        await PendingAsync();

        await RespondAsync(1, _aria.Id.Value, 5, 5, 0, 0x0191);

        Assert.Equal((TargetResultType.Object, _aria.Id), (_results.Single().Kind, _results.Single().Serial));
    }

    [Fact]
    public async Task Handle_AnUnknownSerial_Cancels()
    {
        await PendingAsync();

        await RespondAsync(1, 0x40009999, 0, 0, 0, 0x0EED);

        Assert.Equal(TargetResultType.Canceled, Assert.Single(_results).Kind);
    }

    [Fact]
    public async Task Handle_TheGround_GivesTheLocationAtTheLandHeight()
    {
        await PendingAsync();

        await RespondAsync(1, 0, 6, 6, 99, 0);

        var result = Assert.Single(_results);
        Assert.Equal(
            (TargetResultType.Location, MapType.Felucca, new Point3D(6, 6, 7)),
            (result.Kind, result.Map, result.Location)
        );
    }

    [Fact]
    public async Task Handle_AStatic_GivesTheLocationOnItsTop()
    {
        _tiles.Item(0x0B34, TileFlagType.Surface, 6);
        _map.AddStatic(6, 6, 0x0B34, 2);
        await PendingAsync();

        await RespondAsync(1, 0, 6, 6, 8, 0x0B34);

        Assert.Equal(new Point3D(6, 6, 8), Assert.Single(_results).Location);
    }

    [Theory]
    [InlineData(20, 26)]
    [InlineData(26, 26)]
    [InlineData(0, 6)]
    public async Task Handle_TwoStaticsWithTheSameGraphic_TakesTheOneClicked(sbyte clickedZ, int expectedZ)
    {
        // Two floors of one graphic; High Seas clients send the top of a surface, older ones its base.
        _tiles.Item(0x0B34, TileFlagType.Surface, 6);
        _map.AddStatic(6, 6, 0x0B34, 0);
        _map.AddStatic(6, 6, 0x0B34, 20);
        await PendingAsync();

        await RespondAsync(1, 0, 6, 6, clickedZ, 0x0B34);

        Assert.Equal(new Point3D(6, 6, expectedZ), Assert.Single(_results).Location);
    }

    [Fact]
    public async Task Handle_AStaticThatIsNotThere_Cancels()
    {
        await PendingAsync();

        await RespondAsync(1, 0, 7, 7, 8, 0x0B34);

        Assert.Equal(TargetResultType.Canceled, Assert.Single(_results).Kind);
    }

    [Fact]
    public async Task Handle_OutsideTheMap_Cancels()
    {
        await PendingAsync();

        await RespondAsync(1, 0, 99, 6, 0, 0);

        Assert.Equal(TargetResultType.Canceled, Assert.Single(_results).Kind);
    }

    [Fact]
    public async Task Handle_TheClientCancelled_Cancels()
    {
        await PendingAsync();

        await RespondAsync(1, 0, -1, -1, 0, 0);

        var result = Assert.Single(_results);
        Assert.Equal((TargetResultType.Canceled, TargetCancelType.Canceled), (result.Kind, result.CancelReason));
    }

    [Fact]
    public async Task Handle_AnotherCursorId_IsIgnoredAndTheTargetStaysPending()
    {
        await PendingAsync();

        await RespondAsync(9, 0, 6, 6, 0, 0);

        Assert.Empty(_results);

        await RespondAsync(1, 0, 6, 6, 0, 0);

        Assert.Single(_results);
    }

    [Fact]
    public async Task Handle_NoPendingTarget_IsIgnored()
    {
        await StartAsync();

        await RespondAsync(1, 0, 6, 6, 0, 0);

        Assert.Empty(_results);
    }

    [Fact]
    public async Task Handle_ACallbackThatThrows_LeavesNothingPending()
    {
        await StartAsync();
        await _fixture.ExecuteOnLoopAsync(() => _targets.Begin(
                _session,
                TargetCursorType.Location,
                TargetFlagsType.Neutral,
                (_, _) => throw new InvalidOperationException("boom")
            )
        );

        await RespondAsync(1, 0, 6, 6, 0, 0);
        var pending = true;
        await _fixture.ExecuteOnLoopAsync(() => pending = _session.Get(TargetSessionKeys.State)?.Pending is not null);

        Assert.False(pending);
    }

    private void Record(GameSession session, TargetResult result)
    {
        _results.Add(result);
    }

    private async Task StartAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.CharacterId, _aria.Id));
        _mobiles.EnterWorld(_aria);
        _targets = new(_mobiles, _sender, _fixture.Loop);
    }

    private async Task PendingAsync()
    {
        await StartAsync();
        await _fixture.ExecuteOnLoopAsync(() => _targets.Begin(
                _session,
                TargetCursorType.Location,
                TargetFlagsType.Neutral,
                Record
            )
        );
    }

    private Task RespondAsync(int id, uint serial, short x, short y, sbyte z, ushort graphic)
    {
        var handler = new TargetResponsePacketHandler(_targets, _mobiles, _items, _map, _tiles, _movement);
        var packet = new TargetResponsePacket
        {
            Cursor = TargetCursorType.Location, CursorId = id, Flags = TargetFlagsType.Neutral, Serial = new(serial),
            X = x, Y = y, Z = z, Graphic = graphic
        };

        return _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, packet));
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
