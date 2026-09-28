using DryIoc;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Tests.Support.Sessions;

namespace Moongate.Tests.Server.Ultima.Handlers.General;

public sealed class IgnoredPacketHandlerTests
{
    [Theory]
    [InlineData(0x02, 7)]
    [InlineData(0x06, 5)]
    [InlineData(0x09, 5)]
    [InlineData(0x34, 10)]
    [InlineData(0x72, 5)]
    [InlineData(0xC8, 2)]
    [InlineData(0x05, 5)]
    [InlineData(0x07, 7)]
    [InlineData(0x08, 15)]
    [InlineData(0x13, 10)]
    [InlineData(0x22, 3)]
    [InlineData(0xB5, 64)]
    [InlineData(0xFB, 2)]
    public void FixedFollowUpPackets_DecodeAtTheirLengthOnly(byte opCode, int length)
    {
        var registry = Registry();
        var frame = new byte[length];
        frame[0] = opCode;

        Assert.True(registry.TryGetDescriptor(opCode, PacketDirection.Incoming, out var descriptor));
        Assert.Equal(length, descriptor.FixedLength);
        Assert.True(registry.TryDecode(frame, out _));
        Assert.False(registry.TryDecode(frame.AsSpan(0, length - 1), out _));
    }

    [Theory]
    [InlineData(0xBF, 5)]
    [InlineData(0xD6, 3)]
    [InlineData(0x12, 3)]
    [InlineData(0xE1, 3)]
    public void VariableFollowUpPackets_DecodeWithTheirDeclaredLength(byte opCode, int minimum)
    {
        var registry = Registry();
        var frame = new byte[minimum + 4];
        frame[0] = opCode;
        frame[1] = 0;
        frame[2] = (byte)frame.Length;

        Assert.True(registry.TryGetDescriptor(opCode, PacketDirection.Incoming, out var descriptor));
        Assert.Equal(PacketSizing.Variable, descriptor.Sizing);
        Assert.True(registry.TryDecode(frame, out _));
    }

    [Fact]
    public async Task Handle_SendsNothingAndKeepsTheConnection()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(fixture.Loop).GetOrCreate(fixture.Client);

        await fixture.ExecuteOnLoopAsync(() => new IgnoredPacketHandler<LookRequestPacket>().Handle(session, new LookRequestPacket()));

        Assert.True(fixture.Client.IsConnected);
    }

    private static PacketRegistry Registry()
    {
        var container = new Container();
        container.RegisterIncomingPacket<MoveRequestPacket>();
        container.RegisterIncomingPacket<UseRequestPacket>();
        container.RegisterIncomingPacket<LookRequestPacket>();
        container.RegisterIncomingPacket<MobileQueryPacket>();
        container.RegisterIncomingPacket<WarModeRequestPacket>();
        container.RegisterIncomingPacket<UpdateRangePacket>();
        container.RegisterIncomingPacket<ExtendedCommandPacket>();
        container.RegisterIncomingPacket<QueryPropertiesPacket>();
        container.RegisterIncomingPacket<AttackRequestPacket>();
        container.RegisterIncomingPacket<LiftRequestPacket>();
        container.RegisterIncomingPacket<DropRequestPacket>();
        container.RegisterIncomingPacket<TextCommandPacket>();
        container.RegisterIncomingPacket<EquipRequestPacket>();
        container.RegisterIncomingPacket<ResynchronizeRequestPacket>();
        container.RegisterIncomingPacket<OpenChatWindowPacket>();
        container.RegisterIncomingPacket<ClientTypePacket>();
        container.RegisterIncomingPacket<PublicHouseContentPacket>();
        PacketRegistryFactory.Register(container);

        return container.Resolve<PacketRegistry>();
    }
}
