using Moongate.Network.Packets.Data.Packets;
using Moongate.Server.Attributes;
using Moongate.Server.Handlers;

namespace Moongate.Tests.Server;

public class PacketHandlerRegistrationAttributeTests
{
    [Test]
    public void HandlerRegistrations_ShouldNotContainDuplicateHandlerOpcodePairs()
    {
        var handlerTypes = new[]
        {
            typeof(LoginHandler),
            typeof(CharacterHandler),
            typeof(PingPongHandler),
            typeof(PlayerStatusHandler),
            typeof(MovementHandler),
            typeof(SpeechHandler),
            typeof(ToolTipHandler)
        };

        var pairs = handlerTypes
                    .SelectMany(
                        type => type.GetCustomAttributes(typeof(RegisterPacketHandlerAttribute), false)
                                    .Cast<RegisterPacketHandlerAttribute>()
                                    .Select(attribute => $"{type.FullName}:{attribute.OpCode:X2}")
                    )
                    .ToArray();

        var duplicates = pairs
                         .GroupBy(static pair => pair)
                         .Where(static group => group.Count() > 1)
                         .Select(static group => group.Key)
                         .ToArray();

        Assert.That(duplicates, Is.Empty);
    }

    [Test]
    public void Handlers_ShouldDeclareExpectedPacketRegistrations()
    {
        AssertMappings(
            typeof(LoginHandler),
            PacketDefinition.LoginSeedPacket,
            PacketDefinition.AccountLoginPacket,
            PacketDefinition.ServerSelectPacket,
            PacketDefinition.GameLoginPacket,
            PacketDefinition.LoginCharacterPacket
        );
        AssertMappings(typeof(CharacterHandler), PacketDefinition.CharacterCreationPacket);
        AssertMappings(typeof(PingPongHandler), PacketDefinition.PingMessagePacket);
        AssertMappings(typeof(PlayerStatusHandler), PacketDefinition.GetPlayerStatusPacket);
        AssertMappings(typeof(MovementHandler), PacketDefinition.MoveRequestPacket);
        AssertMappings(typeof(SpeechHandler), PacketDefinition.UnicodeSpeechPacket);
        AssertMappings(typeof(ToolTipHandler), PacketDefinition.MegaClilocPacket);
    }

    private static void AssertMappings(Type handlerType, params byte[] expectedOpcodes)
    {
        var attributes = handlerType
                         .GetCustomAttributes(typeof(RegisterPacketHandlerAttribute), false)
                         .Cast<RegisterPacketHandlerAttribute>()
                         .ToArray();

        var opcodes = attributes
                      .Select(static attribute => attribute.OpCode)
                      .OrderBy(static opcode => opcode)
                      .ToArray();

        Assert.That(opcodes, Is.EqualTo(expectedOpcodes.OrderBy(static opcode => opcode).ToArray()), handlerType.Name);
    }
}
