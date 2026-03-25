using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Modules;

internal sealed class MobileMovementModule
{
    private readonly ISpeechService _speechService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly ISpatialWorldService? _spatialWorldService;
    private readonly IMovementValidationService? _movementValidationService;
    private readonly IPathfindingService? _pathfindingService;
    private readonly IGameEventBusService? _gameEventBusService;
    private readonly IBackgroundJobService? _backgroundJobService;

    public MobileMovementModule(
        ISpeechService speechService,
        IGameNetworkSessionService gameNetworkSessionService,
        ISpatialWorldService? spatialWorldService = null,
        IMovementValidationService? movementValidationService = null,
        IPathfindingService? pathfindingService = null,
        IGameEventBusService? gameEventBusService = null,
        IBackgroundJobService? backgroundJobService = null
    )
    {
        _speechService = speechService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _spatialWorldService = spatialWorldService;
        _movementValidationService = movementValidationService;
        _pathfindingService = pathfindingService;
        _gameEventBusService = gameEventBusService;
        _backgroundJobService = backgroundJobService;
    }

    public bool Teleport(UOMobileEntity mobile, int mapId, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        var proxy = new LuaMobileProxy(
            mobile,
            _speechService,
            _gameNetworkSessionService,
            _spatialWorldService,
            _movementValidationService,
            _pathfindingService,
            _gameEventBusService,
            _backgroundJobService
        );

        return proxy.Teleport(mapId, x, y, z);
    }
}
