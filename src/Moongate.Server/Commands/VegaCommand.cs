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
/// Spawns Vega the cat at target location.
/// </summary>
[RegisterConsoleCommand(
    "vega",
    "Create Vega the cat",
    CommandSourceType.InGame,
    AccountType.Regular
)]
public sealed class VegaCommand : ICommandExecutor
{
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IMobileService _mobileService;
    private readonly ISpatialWorldService _spatialWorldService;

    public VegaCommand(
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
        => await _gameEventBusService.PublishAsync(
               new TargetRequestCursorEvent(
                   context.SessionId,
                   TargetCursorSelectionType.SelectLocation,
                   TargetCursorType.Helpful,
                   callback =>
                   {
                       var mobile = _mobileService.SpawnFromTemplateAsync("vega", callback.Packet.Location, 1)
                                                  .GetAwaiter()
                                                  .GetResult();

                       _spatialWorldService.AddOrUpdateMobile(mobile);
                       context.Print("Vega the cat: {0}", callback.Packet.Location);
                   }
               )
           );
}
