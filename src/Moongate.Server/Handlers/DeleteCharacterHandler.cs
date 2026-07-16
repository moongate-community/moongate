using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Network.Types;
using Moongate.Server.Data;
using Moongate.Server.Interfaces.Accounts;
using Moongate.Server.Interfaces.Network;
using Moongate.Server.Types;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles character deletion (0x83): deletes the character in the requested slot along with
/// everything it owns, then sends the updated character list. A refusal is reported with 0x85 first,
/// as ModernUO does, and the list still follows so the client redraws either way.
/// </summary>
public sealed class DeleteCharacterHandler : IPacketHandler<DeleteCharacterPacket>, IPacketHandlerRegistration
{
    private const byte CharacterSlots = 7;

    private readonly ICharacterService _characterService;

    public DeleteCharacterHandler(ICharacterService characterService)
    {
        _characterService = characterService;
    }

    public void Handle(DeleteCharacterPacket packet, in PacketContext context)
    {
        // Deleting is a character-selection screen action: a session already in the world is asking
        // to delete a character it may well be playing.
        var result = context.Session.State == SessionStateType.InWorld
            ? DeleteResultType.CharBeingPlayed
            : _characterService.DeleteCharacter(context.Session.AccountId, packet.Slot);

        if (result is { } refusal)
        {
            context.Session.Send(new CharacterDeleteResultPacket(refusal));
        }

        var characters = _characterService.GetPlayerCharacters(context.Session.AccountId)
            .Select(character => character.Name)
            .ToList();

        context.Session.Send(new CharacterListUpdatePacket(characters, CharacterSlots));
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
