using System.Buffers.Binary;
using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.System;
using Moongate.Network.Spans;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;

namespace Moongate.Tests.Server.Handlers;

public sealed class PlayerHandlerTests
{
    [Test]
    public async Task HandlePacketAsync_SpyOnClient_ShouldStoreHardwareInfoInSession()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var spatialService = new RegionDataLoaderTestSpatialWorldService();
        var gameNetworkSessionService = new FakeGameNetworkSessionService();
        var handler = new PlayerHandler(spatialService, queue, gameNetworkSessionService);
        var packet = new SpyOnClientPacket();
        var parsed = packet.TryParse(BuildPacketPayload());

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(handled, Is.True);
                Assert.That(session.HardwareInfo, Is.Not.Null);
                Assert.That(session.HardwareInfo!.InstanceId, Is.EqualTo(0x11223344u));
                Assert.That(session.HardwareInfo.OsMajor, Is.EqualTo(10u));
                Assert.That(session.HardwareInfo.VideoCardDescription, Is.EqualTo("NVIDIA RTX"));
                Assert.That(session.HardwareInfo.LanguageCode, Is.EqualTo("ENU"));
            }
        );
    }

    private static byte[] BuildPacketPayload()
    {
        var writer = new SpanWriter(300, true);

        writer.Write((byte)0xD9);
        writer.Write((ushort)0); // placeholder length
        writer.Write((byte)0x02);
        writer.Write(0x11223344u);
        writer.Write(10u);
        writer.Write(0u);
        writer.Write(19045u);
        writer.Write((byte)1);
        writer.Write(6u);
        writer.Write(158u);
        writer.Write(3600u);
        writer.Write((byte)8);
        writer.Write(32768u);
        writer.Write(2560u);
        writer.Write(1440u);
        writer.Write(32u);
        writer.Write((ushort)12);
        writer.Write((ushort)0);
        writer.WriteLittleUni("NVIDIA RTX", 64);
        writer.Write(0x10DEu);
        writer.Write(0x2484u);
        writer.Write(12288u);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.WriteLittleUni("ENU", 4);
        writer.WriteAscii("tail", 64);

        var payload = writer.ToArray();
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(1, 2), (ushort)payload.Length);
        writer.Dispose();

        return payload;
    }
}
