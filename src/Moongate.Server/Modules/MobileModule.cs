using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Descriptors;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("mobile", "Provides helpers to resolve mobiles from scripts.")]

/// <summary>
/// Exposes mobile lookup helpers to Lua scripts.
/// </summary>
public sealed class MobileModule
{
    private static bool _isLuaMobileProxyTypeRegistered;
    private readonly ICharacterService _characterService;
    private readonly ISpeechService _speechService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly ISpatialWorldService? _spatialWorldService;
    private readonly IMovementValidationService? _movementValidationService;
    private readonly IPathfindingService? _pathfindingService;
    private readonly IGameEventBusService? _gameEventBusService;
    private readonly IBackgroundJobService? _backgroundJobService;

    public MobileModule(
        ICharacterService characterService,
        ISpeechService speechService,
        IGameNetworkSessionService gameNetworkSessionService,
        ISpatialWorldService? spatialWorldService = null,
        IMovementValidationService? movementValidationService = null,
        IPathfindingService? pathfindingService = null,
        IGameEventBusService? gameEventBusService = null,
        IBackgroundJobService? backgroundJobService = null
    )
    {
        _characterService = characterService;
        _speechService = speechService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _spatialWorldService = spatialWorldService;
        _movementValidationService = movementValidationService;
        _pathfindingService = pathfindingService;
        _gameEventBusService = gameEventBusService;
        _backgroundJobService = backgroundJobService;
    }

    [ScriptFunction("get", "Gets a mobile reference by character id, or nil when not found.")]
    public LuaMobileProxy? Get(uint characterId)
    {
        if (characterId == 0)
        {
            return null;
        }

        RegisterLuaTypeIfNeeded();
        var mobileId = (Serial)characterId;
        var mobile = TryResolveRuntimeMobile(mobileId) ??
                     _characterService.GetCharacterAsync(mobileId).GetAwaiter().GetResult();

        return mobile is null
                   ? null
                   : new(
                       mobile,
                       _speechService,
                       _gameNetworkSessionService,
                       _spatialWorldService,
                       _movementValidationService,
                       _pathfindingService,
                       _gameEventBusService,
                       _backgroundJobService
                   );
    }

    private static void RegisterLuaTypeIfNeeded()
    {
        if (_isLuaMobileProxyTypeRegistered)
        {
            return;
        }

        var type = typeof(LuaMobileProxy);
        UserData.RegisterType(type, new GenericUserDataDescriptor(type));
        _isLuaMobileProxyTypeRegistered = true;
    }

    private UOMobileEntity? TryResolveRuntimeMobile(Serial mobileId)
    {
        if (_spatialWorldService is null)
        {
            return null;
        }

        foreach (var sector in _spatialWorldService.GetActiveSectors())
        {
            var runtimeMobile = sector.GetEntity<UOMobileEntity>(mobileId);

            if (runtimeMobile is not null)
            {
                return runtimeMobile;
            }
        }

        return null;
    }
}
