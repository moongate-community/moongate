using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Speech;

public sealed class BroadcastFixture : IAsyncDisposable
{
    private readonly List<ControlledNetworkConnection> _connections = [];

    public SessionFixture Network { get; }
    public SessionService Sessions { get; }
    public SectorService Sectors { get; } = TestSectors.Create();
    public MobileService Mobiles { get; }
    public StubPacketSendService Sender { get; } = new();

    private BroadcastFixture(SessionFixture network)
    {
        Network = network;
        Sessions = new(network.Loop);
        Mobiles = new(new StubMovementService(), Sectors);
    }

    public static async Task<BroadcastFixture> CreateAsync()
    {
        return new(await SessionFixture.CreateAsync());
    }

    public async Task<GameSession> AddAsync(long id, bool entered = true, bool connected = true, MapType map = MapType.Trammel)
    {
        var connection = new ControlledNetworkConnection(id);
        _connections.Add(connection);
        var session = Sessions.GetOrCreate(connection);
        await Network.ExecuteOnLoopAsync(() =>
        {
            session.Set(SessionKeys.CharacterId, new Serial((uint)id));

            if (entered)
            {
                Mobiles.EnterWorld(new MobileEntity { Id = new Serial((uint)id), Name = "Player", Map = map });
            }
        });

        if (!connected)
        {
            connection.Complete();
        }

        return session;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            connection.Dispose();
        }

        await Network.DisposeAsync();
    }
}
