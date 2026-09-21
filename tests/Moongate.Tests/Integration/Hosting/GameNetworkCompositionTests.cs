using System.Net;
using DryIoc;
using Moongate.Server.Core.Data.Network;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.Network;

namespace Moongate.Tests.Integration.Hosting;

public sealed class GameNetworkCompositionTests
{
    [Fact]
    public void RawNetwork_ResolvesWithoutGameDependencies()
    {
        using var container = new Container();
        container.RegisterInstance(new NetworkListenerOptions { Endpoints = [new(IPAddress.Loopback, 0)] });
        container.Register<IConnectionService, ConnectionService>(Reuse.Singleton);
        container.Register<INetworkService, NetworkService>(Reuse.Singleton);
        Assert.IsType<NetworkService>(container.Resolve<INetworkService>());
        Assert.False(container.IsRegistered<ISessionService>());
        Assert.False(container.IsRegistered<IGameLoopService>());
    }
}
