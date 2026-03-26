using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Internal.Cursors;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Player;

[RegisterConsoleCommand(
    "kill",
    "Target a mobile and kill it. Usage: .kill",
    CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class KillCommand : ICommandExecutor
{
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IPlayerTargetService _playerTargetService;
    private readonly IMobileService _mobileService;
    private readonly IDeathService _deathService;

    public KillCommand(
        IGameNetworkSessionService gameNetworkSessionService,
        IPlayerTargetService playerTargetService,
        IMobileService mobileService,
        IDeathService deathService
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _playerTargetService = playerTargetService;
        _mobileService = mobileService;
        _deathService = deathService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length != 0)
        {
            context.Print("Usage: .kill");

            return;
        }

        if (!_gameNetworkSessionService.TryGet(context.SessionId, out var session) || session.Character is null)
        {
            context.PrintError("No active character found for kill.");

            return;
        }

        var killer = session.Character;
        await _playerTargetService.SendTargetCursorAsync(
            context.SessionId,
            callback =>
            {
                try
                {
                    HandleTargetSelection(context, callback, killer);
                }
                catch (Exception exception)
                {
                    context.PrintError("Failed to kill target: {0}", exception.Message);
                }
            },
            TargetCursorSelectionType.SelectObject,
            TargetCursorType.Harmful
        );
    }

    private void HandleTargetSelection(
        CommandSystemContext context,
        PendingCursorCallback callback,
        UOMobileEntity killer
    )
    {
        var targetSerial = callback.Packet.ClickedOnId;

        if (targetSerial == Serial.Zero || !targetSerial.IsMobile)
        {
            context.PrintError("Target is not a valid mobile.");

            return;
        }

        var target = ResolveTarget(targetSerial);

        if (target is null)
        {
            context.PrintError("Target mobile not found.");

            return;
        }

        var killed = _deathService.ForceDeathAsync(target, killer).GetAwaiter().GetResult();

        if (!killed)
        {
            context.PrintError("Failed to kill target.");

            return;
        }

        context.Print("Killed {0}.", ResolveTargetName(target));
    }

    private UOMobileEntity? ResolveTarget(Serial targetSerial)
    {
        if (_gameNetworkSessionService.TryGetByCharacterId(targetSerial, out var session) && session.Character is not null)
        {
            return session.Character;
        }

        return _mobileService.GetAsync(targetSerial).GetAwaiter().GetResult();
    }

    private static string ResolveTargetName(UOMobileEntity target)
        => string.IsNullOrWhiteSpace(target.Name) ? target.Id.ToString() : target.Name;
}
