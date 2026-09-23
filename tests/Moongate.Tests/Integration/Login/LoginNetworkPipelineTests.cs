using DryIoc;
using Moongate.Network.Packets.General;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Packets;
using Moongate.Server.Services.Login;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Packets;
using Moongate.Tests.TestSupport.Login;
using Moongate.Tests.TestSupport.Network;

namespace Moongate.Tests.Integration.Login;

public sealed class LoginNetworkPipelineTests
{
    [Fact]
    public async Task BorrowedFrame_DecodesBeforeCallbackReturns_WithoutGameLoop()
    {
        using var container = new Container();
        container.RegisterLoginPacketHandler<PingPacket, RecordingLoginPacketHandler>();
        var connections = new ConnectionService();
        var network = new NetworkServiceStub(connections);
        var sessions = new LoginSessionService();
        var sender = new PacketSendService(connections);
        var dispatcher = new LoginPacketDispatchService(sessions, connections,
            container.Resolve<LoginPacketHandlerRegistry>(), container);
        var server = new LoginServerService(network, connections, sessions, dispatcher, sender);
        using var connection = new ControlledNetworkConnection(1);
        var received = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
        container.Resolve<RecordingLoginPacketHandler>().OnHandle = (_, packet, _) =>
        {
            received.TrySetResult(packet.Sequence);
            return ValueTask.CompletedTask;
        };
        await connections.StartAsync();
        await sender.StartAsync();
        await dispatcher.StartAsync();
        await server.StartAsync();

        try
        {
            network.Accept(connection);
            var frame = new byte[] { 0x73, 42 };
            network.Receive(connection, frame);
            Array.Fill<byte>(frame, 0xFF);

            Assert.Equal(42, await received.Task.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.True(sessions.TryGet(1, out _));
            network.Close(connection);
            await Task.Delay(50);
            Assert.False(sessions.TryGet(1, out _));
        }
        finally
        {
            await server.StopAsync();
            await dispatcher.StopAsync();
            await sender.StopAsync();
            await connections.StopAsync();
        }
    }
}
