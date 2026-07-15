using Moongate.Network.Packets.Incoming;
using Moongate.Server.Data;
using Moongate.Server.Interfaces.Accounts;
using Moongate.Server.Interfaces.Network;
using Moongate.Server.Interfaces.World;
using Serilog;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles play character (0x5D): resolves the chosen slot against the account's characters, attaches
/// it to the session, and sends the enter-world burst. An out-of-range slot drops the connection.
/// </summary>
public sealed class CharacterSelectHandler : IPacketHandler<CharacterSelectPacket>, IPacketHandlerRegistration
{
    private readonly ILogger _logger = Log.ForContext<CharacterSelectHandler>();

    private readonly ICharacterService _characterService;
    private readonly IEnterWorldService _enterWorld;

    public CharacterSelectHandler(ICharacterService characterService, IEnterWorldService enterWorld)
    {
        _characterService = characterService;
        _enterWorld = enterWorld;
    }

    public void Handle(CharacterSelectPacket packet, in PacketContext context)
    {
        var characters = _characterService.GetPlayerCharacters(context.Session.AccountId).ToList();

        if (packet.Slot < 0 || packet.Slot >= characters.Count)
        {
            _logger.Warning(
                "Character slot {Slot} out of range (have {Count}) for account {AccountId}; dropping session",
                packet.Slot,
                characters.Count,
                context.Session.AccountId
            );

            context.Session.Disconnect();

            return;
        }

        var character = characters[packet.Slot];

        context.Session.SetCharacter(character);
        _enterWorld.SendEnterWorld(context.Session, character);
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
