using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Network.Types;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Network;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles character deletion (0x83): deletes the character in the requested slot along with
/// everything it owns, then sends the updated character list. Whether the deletion is allowed — the
/// slot exists, nobody is playing that character — is the character service's call. A refusal is
/// reported with 0x85 first, as ModernUO does, and the list still follows so the client redraws either
/// way.
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
        var result = _characterService.DeleteCharacter(context.Session.AccountId, packet.Slot);

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
