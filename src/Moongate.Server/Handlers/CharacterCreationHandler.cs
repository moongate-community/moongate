using Moongate.Network.Packets.Incoming;
using Moongate.Server.Data;
using Moongate.Server.Interfaces.Accounts;
using Moongate.Server.Interfaces.Network;
using Moongate.Server.Interfaces.World;
using Serilog;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles character creation (0xF8): builds and persists the player mobile for the session's account,
/// attaches it to the session, and sends the enter-world burst so the new character enters the game.
/// </summary>
public sealed class CharacterCreationHandler : IPacketHandler<CharacterCreationPacket>, IPacketHandlerRegistration
{
    private readonly ILogger _logger = Log.ForContext<CharacterCreationHandler>();

    private readonly ICharacterService _characterService;
    private readonly IEnterWorldService _enterWorld;

    public CharacterCreationHandler(ICharacterService characterService, IEnterWorldService enterWorld)
    {
        _characterService = characterService;
        _enterWorld = enterWorld;
    }

    public void Handle(CharacterCreationPacket packet, in PacketContext context)
    {
        var character = _characterService.CreateCharacter(context.Session.AccountId, packet);

        context.Session.SetCharacter(character);
        _enterWorld.SendEnterWorld(context.Session, character);
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
