using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Handlers.Tooltips;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Handlers.Tooltips;

public sealed class TooltipRequestHandlersTests : IAsyncDisposable
{
    private static readonly Serial Aria = new(0x00000002);
    private static readonly Serial Far = new(0x00000003);

    private readonly ItemService _items;
    private readonly MobileService _mobiles;
    private readonly TooltipService _tooltips;
    private readonly StubPacketSendService _sender = new();
    private readonly ItemEntity _gold = new() { Id = new(0x40000010), TemplateId = "gold", ItemId = 0x0EED, Amount = 1 };

    private SessionFixture _fixture = null!;
    private GameSession _session = null!;

    public TooltipRequestHandlersTests()
    {
        var sectors = TestSectors.Create();
        _items = TestItems.Create(sectors);
        _mobiles = new(new StubMovementService(), sectors);
        _mobiles.EnterWorld(new() { Id = Aria, Name = "Aria", Body = 401, Map = MapType.Trammel, Location = new Point3D(1000, 1000, 0) });
        _mobiles.EnterWorld(new() { Id = Far, Name = "Far", Body = 400, Map = MapType.Trammel, Location = new Point3D(1500, 1000, 0) });
        _items.Add([_gold]);
        _items.PlaceOnGround(_gold, MapType.Trammel, new Point3D(1001, 1000, 0));
        _tooltips = TestTooltips.Create(_items, _mobiles);
    }

    [Fact]
    public async Task QueryProperties_AnswersEveryVisibleSerialAndSkipsTheOthers()
    {
        await StartAsync();
        var handler = new QueryPropertiesPacketHandler(_tooltips, _sender);

        await _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, Query(_gold.Id, Far, Aria)));

        Assert.Equal([_gold.Id, Aria], _sender.Sent.Select(packet => Assert.IsType<PropertyListPacket>(packet).Serial));
    }

    [Fact]
    public async Task ExtendedCommand_0x10_AnswersTheTooltip()
    {
        await StartAsync();
        var handler = new ExtendedCommandPacketHandler(_tooltips, _sender);

        await _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, Extended(0x10, _gold.Id)));

        Assert.Equal(_gold.Id, Assert.IsType<PropertyListPacket>(Assert.Single(_sender.Sent)).Serial);
    }

    [Fact]
    public async Task ExtendedCommand_AnotherSubcommand_SendsNothing()
    {
        await StartAsync();
        var handler = new ExtendedCommandPacketHandler(_tooltips, _sender);

        await _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, Extended(0x0B, new Serial(0x656E7500))));

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Look_AnItem_ShowsItsTooltipNameOverIt()
    {
        await StartAsync();
        var handler = new LookRequestPacketHandler(_tooltips, _items, _mobiles, _sender);

        await _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, new LookRequestPacket { Target = _gold.Id }));

        var label = Assert.IsType<LocalizedMessagePacket>(Assert.Single(_sender.Sent));
        Assert.Equal((_gold.Id, 0x0EED, 1020000 + 0x0EED), (label.Serial, label.Graphic, label.Cliloc));
    }

    [Fact]
    public async Task Look_AMobile_ShowsItsNameWithItsBody()
    {
        await StartAsync();
        var handler = new LookRequestPacketHandler(_tooltips, _items, _mobiles, _sender);

        await _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, new LookRequestPacket { Target = Aria }));

        var label = Assert.IsType<LocalizedMessagePacket>(Assert.Single(_sender.Sent));
        Assert.Equal((Aria, 401, 1050045, "Aria"), (label.Serial, label.Graphic, label.Cliloc, label.Name));
    }

    [Fact]
    public async Task Look_SomethingOutOfView_SendsNothing()
    {
        await StartAsync();
        var handler = new LookRequestPacketHandler(_tooltips, _items, _mobiles, _sender);

        await _fixture.ExecuteOnLoopAsync(() => handler.Handle(_session, new LookRequestPacket { Target = Far }));

        Assert.Empty(_sender.Sent);
    }

    private static QueryPropertiesPacket Query(params Serial[] serials)
    {
        var data = new byte[3 + serials.Length * 4];
        data[0] = 0xD6;
        data[1] = (byte)(data.Length >> 8);
        data[2] = (byte)data.Length;

        for (var i = 0; i < serials.Length; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(3 + i * 4), serials[i].Value);
        }

        Assert.True(QueryPropertiesPacket.TryParse(data, out var packet));

        return packet;
    }

    private static ExtendedCommandPacket Extended(ushort subcommand, Serial serial)
    {
        var data = new byte[9];
        data[0] = 0xBF;
        data[2] = 9;
        data[4] = (byte)subcommand;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(5), serial.Value);
        Assert.True(ExtendedCommandPacket.TryParse(data, out var packet));

        return packet;
    }

    private async Task StartAsync()
    {
        _fixture = await SessionFixture.CreateAsync();
        _session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.CharacterId, Aria));
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
