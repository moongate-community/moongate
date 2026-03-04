using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Player;

[RegisterConsoleCommand(
    "where",
    "Prints the current location of the player. Usage: .where",
    CommandSourceType.InGame,
    AccountType.Regular
)]
public class WhereCommand : ICommandExecutor
{
    private readonly ISpeechService _speechService;
    private readonly IMobileService _mobileService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public WhereCommand(
        ISpeechService speechService,
        IMobileService mobileService,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _speechService = speechService;
        _mobileService = mobileService;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (_gameNetworkSessionService.TryGet(context.SessionId, out var gameSession))
        {
            var character = await _mobileService.GetAsync(gameSession.CharacterId);

            _speechService.SendMessageFromServerAsync(
                gameSession,
                $"You are at X: {character.Location.X}, Y: {character.Location.Y}, Z: {character.Location.Z} on map {Map.GetMap(character.MapId).Name}."
            );
        }

    }
}
