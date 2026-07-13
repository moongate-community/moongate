using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Network.Types;
using Moongate.Server.Data;
using Moongate.Server.Interfaces;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Handlers;

/// <summary>Handles game server login (0x91): validates the handoff key and sends the character list.</summary>
public sealed class GameServerLoginHandler : IPacketHandler<GameServerLoginPacket>, IPacketHandlerRegistration
{
    private const byte CharacterSlots = 7;

    private readonly ILogger _logger = Log.ForContext<GameServerLoginHandler>();

    private readonly IPendingLoginStore _pendingLogins;
    private readonly IStartingCityService _cities;
    private readonly IAccountService _accountService;
    private readonly ICharacterService _characterService;

    public GameServerLoginHandler(
        IPendingLoginStore pendingLogins,
        IStartingCityService cities,
        IAccountService accountService,
        ICharacterService characterService
    )
    {
        _pendingLogins = pendingLogins;
        _cities = cities;
        _accountService = accountService;
        _characterService = characterService;
    }

    public void Handle(GameServerLoginPacket packet, in PacketContext context)
    {
        if (!_pendingLogins.TryTake(packet.AuthKey, out var pending))
        {
            context.Session.Send(new LoginDeniedPacket(LoginDeniedReasonType.CommunicationProblem));

            return;
        }

        context.Session.MarkAuthenticated(pending.Username);
        var accountId = _accountService.GetAccountIdByUsername(pending.Username);

        if (accountId.HasValue)
        {
            context.Session.SetAccountId(accountId.Value);
        }
        else
        {
            _logger.Error("Account with username {PendingUsername} not found", pending.Username);

            throw new InvalidOperationException($"Account with username {pending.Username} not found");
        }
        var characters = _characterService.GetPlayerCharacters(accountId.Value).Select(s => s.Name);

        context.Session.Send(
            new CharacterListPacket([.. characters], _cities.All, CharacterSlots, CharacterListFlagType.Modern)
        );
    }

    public void Register(INetworkService network)
    {
        network.RegisterHandler(this);
    }
}
