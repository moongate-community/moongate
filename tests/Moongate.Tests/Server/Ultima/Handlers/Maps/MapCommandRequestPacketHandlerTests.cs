using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Maps;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Types.MapItems;
using Moongate.Tests.Support.Sessions;

namespace Moongate.Tests.Server.Ultima.Handlers.Maps;

public sealed class MapCommandRequestPacketHandlerTests
{
    [Fact]
    public async Task Handle_GivesThePacketToTheMapService()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(fixture.Loop).GetOrCreate(fixture.Client);
        var maps = new RecordingMaps();
        var packet = new MapCommandRequestPacket { Serial = 0x40000002, Command = MapCommandType.AddPin, Number = 0, X = 1, Y = 2 };

        new MapCommandRequestPacketHandler(maps).Handle(session, packet);

        Assert.Same(packet, Assert.Single(maps.Handled));
    }

    private sealed class RecordingMaps : IMapDisplayService
    {
        public List<MapCommandRequestPacket> Handled { get; } = [];

        public bool Display(GameSession session, ItemEntity map)
        {
            return false;
        }

        public void Handle(GameSession session, MapCommandRequestPacket packet)
        {
            Handled.Add(packet);
        }
    }
}
