using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.Characters;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Characters;

/// <summary>
///     Creates the character the classic client asks for (0xF8).
/// </summary>
public sealed class CreateCharacterPacketHandler : IAsyncPacketHandler<CreateCharacterPacket>
{
    private readonly ILogger _logger = Log.ForContext<CreateCharacterPacketHandler>();
    private readonly ICharacterService _characters;
    private readonly ICharacterEnterWorldService _enter;

    public CreateCharacterPacketHandler(ICharacterService characters, ICharacterEnterWorldService enter)
    {
        _characters = characters;
        _enter = enter;
    }

    public ValueTask HandleAsync(PacketContext context, CreateCharacterPacket packet, CancellationToken cancellationToken)
    {
        var request = new CharacterCreationRequest
        {
            Slot = packet.CharacterSlot,
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
            PantsHue = packet.PantsHue
        };

        return CharacterCreationReply.HandleAsync(context, _characters, _enter, request, _logger, cancellationToken);
    }
}
