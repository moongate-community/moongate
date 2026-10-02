using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.Characters;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Characters;

/// <summary>
///     Creates the character the Enhanced Client asks for (0x8D). The face and shirt style are not kept, and the client
///     has no pants choice, so the pants keep their template hue.
/// </summary>
public sealed class CreateCharacterEnhancedPacketHandler : IAsyncPacketHandler<CreateCharacterEnhancedPacket>
{
    private readonly ILogger _logger = Log.ForContext<CreateCharacterEnhancedPacketHandler>();
    private readonly ICharacterService _characters;
    private readonly ICharacterEnterWorldService _enter;

    public CreateCharacterEnhancedPacketHandler(ICharacterService characters, ICharacterEnterWorldService enter)
    {
        _characters = characters;
        _enter = enter;
    }

    public ValueTask HandleAsync(
        PacketContext context,
        CreateCharacterEnhancedPacket packet,
        CancellationToken cancellationToken
    )
    {
        var request = new CharacterCreationRequest
        {
            // A slot beyond int is out of range anyway; the service refuses it.
            Slot = packet.CharacterSlot > int.MaxValue ? int.MaxValue : (int)packet.CharacterSlot,
            Name = packet.Name,
            Profession = packet.Profession,
            StartingCity = packet.StartingCity,
            Gender = packet.Gender,
            Race = packet.Race,
            Strength = packet.Strength,
            Dexterity = packet.Dexterity,
            Intelligence = packet.Intelligence,
            Skills = packet.Skills,
            SkinHue = packet.SkinHue,
            HairStyle = packet.HairStyle,
            HairHue = packet.HairHue,
            BeardStyle = packet.BeardStyle,
            BeardHue = packet.BeardHue,
            ShirtHue = packet.ShirtHue,
            PantsHue = new Hue(0)
        };

        return CharacterCreationReply.HandleAsync(context, _characters, _enter, request, _logger, cancellationToken);
    }
}
