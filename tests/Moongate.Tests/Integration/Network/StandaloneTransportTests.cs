using System.Net;
using System.Net.Sockets;
using DryIoc;
using Moongate.Core.Directories;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Network;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Integration.Network;

public sealed class StandaloneTransportTests
{
    [Fact]
    public async Task Standalone_LoginAndGameListenersOwnIndependentConnections()
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig
        {
            Mode = ServerMode.Standalone,
            Network = new() { ListenAddress = IPAddress.Loopback.ToString(), LoginPort = 0, GamePort = 0 },
            RealmDirectory = new() { AdvertisedPort = 2595 }
        };
        container.RegisterInstance(config);
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        ServerRoleRegistration.Register(container, config, directories);
        var loginConnections = container.Resolve<ILoginConnectionService>();
        var gameConnections = container.Resolve<IConnectionService>();
        var loginNetwork = Assert.IsType<NetworkService>(container.Resolve<ILoginNetworkService>());
        var gameNetwork = Assert.IsType<NetworkService>(container.Resolve<INetworkService>());
        var loginAccepted = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gameAccepted = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loginReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gameReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        loginNetwork.ConnectionAccepted += (_, args) => loginAccepted.TrySetResult(args.Connection.SessionId);
        gameNetwork.ConnectionAccepted += (_, args) => gameAccepted.TrySetResult(args.Connection.SessionId);
        loginNetwork.DataReceived += (_, args) => loginReceived.TrySetResult(args.Data.ToArray());
        gameNetwork.DataReceived += (_, args) => gameReceived.TrySetResult(args.Data.ToArray());

        await Task.WhenAll(loginConnections.StartAsync(), gameConnections.StartAsync());

        try
        {
            await Task.WhenAll(loginNetwork.StartAsync(), gameNetwork.StartAsync());
            var loginEndpoint = Assert.Single(loginNetwork.Listeners).Endpoint;
            var gameEndpoint = Assert.Single(gameNetwork.Listeners).Endpoint;
            Assert.NotEqual(loginEndpoint.Port, gameEndpoint.Port);

            using var loginClient = new TcpClient();
            await loginClient.ConnectAsync(loginEndpoint);
            var loginId = await loginAccepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(loginConnections.TryGet(loginId, out var loginConnection));

            using var gameClient = new TcpClient();
            await gameClient.ConnectAsync(gameEndpoint);
            var gameId = await gameAccepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(gameConnections.TryGet(gameId, out var gameConnection));
            Assert.NotSame(loginConnection, gameConnection);
            Assert.Equal(1, loginConnections.Count);
            Assert.Equal(1, gameConnections.Count);

            await loginClient.GetStream().WriteAsync(new byte[] { 0x73, 0x2A });
            Assert.Equal(new byte[] { 0x73, 0x2A },
                await loginReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(gameReceived.Task.IsCompleted);

            await gameClient.GetStream().WriteAsync(new byte[] { 0x73, 0x3B });
            Assert.Equal(new byte[] { 0x73, 0x3B },
                await gameReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            await Task.WhenAll(loginNetwork.StopAsync(), gameNetwork.StopAsync()).WaitAsync(TimeSpan.FromSeconds(5));
            await Task.WhenAll(loginConnections.StopAsync(), gameConnections.StopAsync())
                      .WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
