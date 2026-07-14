using Moongate.Network.Packets.Incoming;
using Moongate.Server.Data;
using Moongate.Server.Interfaces.Accounts;
using Moongate.Server.Interfaces.Network;
using Serilog;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles character creation (0xF8): builds and persists the player mobile for the session's account
/// and attaches it to the session. Entering the game world is a later step.
/// </summary>
public sealed class CharacterCreationHandler : IPacketHandler<CharacterCreationPacket>, IPacketHandlerRegistration
{
    private readonly ILogger _logger = Log.ForContext<CharacterCreationHandler>();

    private readonly ICharacterService _characterService;

    public CharacterCreationHandler(ICharacterService characterService)
    {
        _characterService = characterService;
    }

    public void Handle(CharacterCreationPacket packet, in PacketContext context)
    {
        var character = _characterService.CreateCharacter(context.Session.AccountId, packet);

        context.Session.SetCharacter(character);
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
