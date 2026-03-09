using DryIoc;
using Moongate.Abstractions.Extensions;
using Moongate.Abstractions.Types;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Interfaces.Services.Characters;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.Server.Interfaces.Services.Network;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.Characters;
using Moongate.Server.Services.Console;
using Moongate.Server.Services.EventLoop;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.Files;
using Moongate.Server.Services.Metrics;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Scripting;
using Moongate.Server.Services.Spatial;
using Moongate.Server.Services.World;

namespace Moongate.Server.Extensions.Bootstrap;

/// <summary>
/// Registers host-managed Moongate services with startup priorities.
/// </summary>
public static class AddBootstrapHostedServicesExtension
{
    /// <summary>
    /// Registers host lifecycle services used during bootstrap run loop.
    /// </summary>
    public static Container AddBootstrapHostedServices(this Container container)
    {
        container.RegisterMoongateService<IPersistenceService, PersistenceService>(ServicePriority.Persistence);
        container.RegisterMoongateService<IFileLoaderService, FileLoaderService>(ServicePriority.FileLoader);
        container.RegisterMoongateService<IGameLoopService, GameLoopService>(ServicePriority.GameLoop);
        container.RegisterMoongateService<IWeatherService, WeatherService>(ServicePriority.GameLoop);
        container.RegisterMoongateService<ILightService, LightService>(ServicePriority.GameLoop);

        container.RegisterMoongateService<ICharacterPositionPersistenceService, CharacterPositionPersistenceService>(
            ServicePriority.CharacterPositionPersistence
        );
        container.RegisterDelegate<ICommandSystemService>(
            resolver => resolver.Resolve<CommandSystemService>(),
            Reuse.Singleton
        );
        container.RegisterMoongateService<IConsoleCommandService, ConsoleCommandService>(ServicePriority.ConsoleCommand);
        container.RegisterMoongateService<IMetricsCollectionService, MetricsCollectionService>(
            ServicePriority.MetricsCollection
        );
        container.RegisterMoongateService<IGameEventScriptBridgeService, GameEventScriptBridgeService>(
            ServicePriority.GameEventScriptBridge
        );
        container.RegisterMoongateService<INetworkService, NetworkService>(ServicePriority.Network);
        container.RegisterMoongateService<IScriptEngineService, LuaScriptEngineService>(ServicePriority.ScriptEngine);
        container.RegisterMoongateService<ILuaBrainRunner, LuaBrainRunner>(ServicePriority.ScriptEngine);
        container.RegisterMoongateService<ISpawnService, SpawnService>(ServicePriority.EventListener);

        return container;
    }
}
