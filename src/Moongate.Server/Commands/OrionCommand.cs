using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands;

/// <summary>
/// Spawns Orion the cat at target location.
/// </summary>
[RegisterConsoleCommand(
    "orion",
    "Create a cat, beautiful cat",
    CommandSourceType.InGame,
    AccountType.Regular
)]
public sealed class OrionCommand : ICommandExecutor
{
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IMobileService _mobileService;
    private readonly ISpatialWorldService _spatialWorldService;

    public OrionCommand(
        IGameEventBusService gameEventBusService,
        IMobileService mobileService,
        ISpatialWorldService spatialWorldService
    )
    {
        _gameEventBusService = gameEventBusService;
        _mobileService = mobileService;
        _spatialWorldService = spatialWorldService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        await _gameEventBusService.PublishAsync(
            new TargetRequestCursorEvent(
                context.SessionId,
                TargetCursorSelectionType.SelectLocation,
                TargetCursorType.Helpful,
                callback =>
                {
                    var mobile = _mobileService.SpawnFromTemplateAsync("orione", callback.Packet.Location, 1)
                                               .GetAwaiter()
                                               .GetResult();

                    _spatialWorldService.AddOrUpdateMobile(mobile);
                    context.Print("Orion the cat: {0}", callback.Packet.Location);
                }
            )
        );
    }
}
