using Moongate.Network.Packets.Incoming;
using Moongate.Persistence.Entities;
using Moongate.Server.Data;
using Moongate.Server.Interfaces;
using Moongate.UO.Data.Hues;
using Serilog;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles character creation (0xF8): builds the player mobile from the creation packet and attaches
/// it to the session. The mobile is not persisted yet; entering the game world is a later step.
/// </summary>
public sealed class CharacterCreationHandler : IPacketHandler<CharacterCreationPacket>, IPacketHandlerRegistration
{
    private readonly ILogger _logger = Log.ForContext<CharacterCreationHandler>();

    public void Handle(CharacterCreationPacket packet, in PacketContext context)
    {
        var character = CreateCharacter(packet);

        context.Session.SetCharacter(character);

        _logger.Information(
            "Character created (not persisted) for session {SessionId}: {Name} gender {Gender} race {Race} " +
            "skin 0x{Skin:X4} hair 0x{HairStyle:X4}/0x{HairHue:X4} facial hair 0x{FacialStyle:X4}/0x{FacialHue:X4}",
            context.Session.SessionId,
            character.Name,
            packet.Gender,
            packet.Race,
            character.SkinHue.Value,
            character.HairStyle,
            character.HairHue.Value,
            character.FacialHairStyle,
            character.FacialHairHue.Value
        );
    }

    public static MobileEntity CreateCharacter(CharacterCreationPacket packet)
    {
        return new MobileEntity
        {
            Name = packet.Name,
            Gender = packet.Gender,
            Race = packet.Race,
            ProfessionId = packet.ProfessionId,
            Strength = packet.Strength,
            Dexterity = packet.Dexterity,
            Intelligence = packet.Intelligence,
            SkinHue = new Hue((ushort)packet.SkinHue),
            HairStyle = (ushort)packet.HairStyle,
            HairHue = new Hue((ushort)packet.HairHue),
            FacialHairStyle = (ushort)packet.FacialHairStyle,
            FacialHairHue = new Hue((ushort)packet.FacialHairHue)
        };
    }

    public void Register(INetworkService network)
    {
        network.RegisterHandler(this);
    }
}
