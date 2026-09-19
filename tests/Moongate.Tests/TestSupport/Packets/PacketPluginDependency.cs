using Moongate.Server.Core.Interfaces.Services;
using Moongate.Tests.Support.GameLoop;

namespace Moongate.Tests.TestSupport.Packets;

public sealed class PacketPluginDependency : IMoongateStartupService
{
    private readonly IGameLoopService _loop;
    private readonly ISessionService _sessions;
    private readonly IPacketDispatchService _dispatcher;
    private readonly IPacketSendService _sender;
    private readonly IConnectionService _connections;
    public bool Started { get; private set; }
    public bool Stopped { get; private set; }

    public PacketPluginDependency(IGameLoopService loop, ISessionService sessions, IPacketDispatchService dispatcher, IPacketSendService sender, IConnectionService connections)
    {
        _connections = connections;
        _loop = loop;
        _sessions = sessions;
        _dispatcher = dispatcher;
        _sender = sender;
    }

    public Task StartAsync()
    {
        Assert.True(_loop.TryPost(new ActionGameLoopWorkItem(() => { })));
        Started = true;
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Assert.Empty(_sessions.GetAll());
        Assert.Empty(_connections.GetAll());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _connections.StartAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _dispatcher.StartAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sender.StartAsync());
        Assert.True(_loop.TryPost(new ActionGameLoopWorkItem(() => { })));
        Stopped = true;
    }
}
