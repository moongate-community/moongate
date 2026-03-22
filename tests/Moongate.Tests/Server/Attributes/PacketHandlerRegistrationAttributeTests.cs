using Moongate.Network.Packets.Data.Packets;
using Moongate.Server.Attributes;
using Moongate.Server.Handlers;

namespace Moongate.Tests.Server.Attributes;

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
            typeof(KrriosClientSpecialHandler),
            typeof(UpdateViewPublicHouseContentsHandler),
            typeof(BulletinBoardHandler),
            typeof(ChatHandler),
            typeof(SpeechHandler),
            typeof(DyeWindowHandler),
            typeof(GumpHandler),
            typeof(ToolTipHandler),
            typeof(ItemHandler)
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
            PacketDefinition.LoginCharacterPacket,
            PacketDefinition.ClientTypePacket,
            PacketDefinition.ClientVersionPacket
        );
        AssertMappings(
            typeof(CharacterHandler),
            PacketDefinition.CharacterCreationPacket,
            PacketDefinition.RequestWarModePacket
        );
        AssertMappings(typeof(PingPongHandler), PacketDefinition.PingMessagePacket);
        AssertMappings(typeof(PlayerStatusHandler), PacketDefinition.GetPlayerStatusPacket);
        AssertMappings(typeof(MovementHandler), PacketDefinition.MoveRequestPacket);
        AssertMappings(typeof(KrriosClientSpecialHandler), PacketDefinition.NewMovementRequestPacket);
        AssertMappings(typeof(UpdateViewPublicHouseContentsHandler), PacketDefinition.UpdateViewPublicHouseContentsPacket);
        AssertMappings(typeof(BulletinBoardHandler), PacketDefinition.BulletinBoardMessagesPacket);
        AssertMappings(typeof(ChatHandler), PacketDefinition.ChatTextPacket, PacketDefinition.OpenChatWindowPacket);
        AssertMappings(typeof(SpeechHandler), PacketDefinition.UnicodeSpeechPacket);
        AssertMappings(typeof(DyeWindowHandler), PacketDefinition.DyeWindowPacket);
        AssertMappings(typeof(GumpHandler), PacketDefinition.GumpMenuSelectionPacket);
        AssertMappings(typeof(ToolTipHandler), PacketDefinition.MegaClilocPacket);
        AssertMappings(
            typeof(ItemHandler),
            PacketDefinition.BookHeaderNewPacket,
            PacketDefinition.BookPagesPacket,
            PacketDefinition.DropItemPacket,
            PacketDefinition.DropWearItemPacket,
            PacketDefinition.PickUpItemPacket,
            PacketDefinition.SingleClickPacket,
            PacketDefinition.DoubleClickPacket
        );
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
