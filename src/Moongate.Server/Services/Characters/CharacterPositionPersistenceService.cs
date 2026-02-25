using Moongate.Abstractions.Services.Base;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Interfaces.Services.Characters;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Utils;
using Serilog;

namespace Moongate.Server.Services.Characters;

/// <summary>
/// Persists mobile runtime position changes to storage with lightweight write throttling.
/// </summary>
public sealed class CharacterPositionPersistenceService
    : BaseMoongateService, ICharacterPositionPersistenceService, IGameEventListener<MobilePositionChangedEvent>
{
    private const long PersistThrottleMs = 500;

    private readonly ILogger _logger = Log.ForContext<CharacterPositionPersistenceService>();
    private readonly Lock _sync = new();
    private readonly Dictionary<long, long> _lastPersistBySessionId = [];
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IPersistenceService _persistenceService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CharacterPositionPersistenceService"/> class.
    /// </summary>
    public CharacterPositionPersistenceService(
        IGameEventBusService gameEventBusService,
        IGameNetworkSessionService gameNetworkSessionService,
        IPersistenceService persistenceService
    )
    {
        _gameEventBusService = gameEventBusService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _persistenceService = persistenceService;
    }

    /// <inheritdoc />
    public override Task StartAsync()
    {
        _gameEventBusService.RegisterListener<MobilePositionChangedEvent>(this);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task HandleAsync(MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        if (!_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session) ||
            session.Character is null ||
            session.CharacterId != gameEvent.MobileId)
        {
            return;
        }

        var now = Environment.TickCount64;
        var sectorChanged = HasSectorChanged(gameEvent.OldLocation, gameEvent.NewLocation);
        var shouldPersist = sectorChanged;

        if (!shouldPersist)
        {
            lock (_sync)
            {
                shouldPersist = !_lastPersistBySessionId.TryGetValue(gameEvent.SessionId, out var lastPersistTs) ||
                                now - lastPersistTs >= PersistThrottleMs;
            }
        }

        if (!shouldPersist)
        {
            return;
        }

        session.Character.Location = gameEvent.NewLocation;
        session.Character.MapId = gameEvent.MapId;
        await _persistenceService.UnitOfWork.Mobiles.UpsertAsync(session.Character, cancellationToken);

        lock (_sync)
        {
            _lastPersistBySessionId[gameEvent.SessionId] = now;
        }

        _logger.Verbose(
            "Persisted character position Session={SessionId} Mobile={MobileId} Map={MapId} Location={Location}",
            gameEvent.SessionId,
            gameEvent.MobileId,
            gameEvent.MapId,
            gameEvent.NewLocation
        );
    }

    private static bool HasSectorChanged(Point3D oldLocation, Point3D newLocation)
    {
        var oldSectorX = oldLocation.X >> MapSectorConsts.SectorShift;
        var oldSectorY = oldLocation.Y >> MapSectorConsts.SectorShift;
        var newSectorX = newLocation.X >> MapSectorConsts.SectorShift;
        var newSectorY = newLocation.Y >> MapSectorConsts.SectorShift;

        return oldSectorX != newSectorX || oldSectorY != newSectorY;
    }
}
