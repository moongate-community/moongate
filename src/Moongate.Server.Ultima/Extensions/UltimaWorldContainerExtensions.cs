using DryIoc;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Interfaces.Sessions;
using Moongate.Server.Ultima.Characters;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Services.Diagnostics;
using Moongate.Server.Ultima.Services;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the services of the live world: templates, factories, mobiles, NPCs, items, the world view, characters, speech, light, weather, regions, decoration and the Lua modules, with their startup order.
/// </summary>
public static class UltimaWorldContainerExtensions
{
    /// <summary>
    ///     Registers the services of the live world: templates, factories, mobiles, NPCs, items, the world view, characters, speech, light, weather, regions, decoration and the Lua modules, with their startup order.
    /// </summary>
    public static Container AddUltimaWorldServices(this Container container)
    {
        container.Register<ILocalizationService, LocalizationService>(Reuse.Singleton);
        container.Register<INameService, NameService>(Reuse.Singleton);
        container.Register<IItemTemplateService, ItemTemplateService>(Reuse.Singleton);
        container.Register<IMobileTemplateService, MobileTemplateService>(Reuse.Singleton);
        container.Register<IContainerLayoutService, ContainerLayoutService>(Reuse.Singleton);
        container.Register<IItemFactoryService, ItemFactoryService>(Reuse.Singleton);
        container.Register<IDecorationsLoader, DecorationsLoader>(Reuse.Singleton);
        container.Register<IDoorGeneratorService, DoorGeneratorService>(Reuse.Singleton);
        container.Register<IDecorationService, DecorationService>(Reuse.Singleton);
        container.Register<IClockService, ClockService>(Reuse.Singleton);
        container.Register<IRegionService, RegionService>(Reuse.Singleton);
        container.Register<ILootService, LootService>(Reuse.Singleton);
        container.Register<IMobileFactoryService, MobileFactoryService>(Reuse.Singleton);
        container.Register<INpcTickService, NpcTickService>(Reuse.Singleton);
        container.Register<IWeightService, WeightService>(Reuse.Singleton);
        container.Register<IFatigueService, FatigueService>(Reuse.Singleton);
        container.AddMoongateService<IRegenerationService, RegenerationService>(12);
        container.AddMoongateService<IHungerService, HungerService>(12);
        container.AddMoongateService<ICrimeService, CrimeService>(12);
        container.AddMoongateService<IGuardService, GuardService>(12);
        // After the data loaders: the cells come from data/jail.toml; its sentences are read from the world database.
        container.AddMoongateService<IJailService, JailService>(12);
        // After the script engine (70) and its bootstrap: the mobile scripts load into the running engine.
        container.AddMoongateService<NpcScriptService>(LuaScriptEngineService.StartupPriority + 5);
        container.RegisterDelegate<INpcThinker>(resolver => resolver.Resolve<NpcScriptService>(), Reuse.Singleton);
        container.RegisterDelegate<INpcScriptService>(resolver => resolver.Resolve<NpcScriptService>(), Reuse.Singleton);
        container.Register<INpcSpeechListener, NpcHearingService>(Reuse.Singleton);
        container.Register<IItemSpeechListener, ItemHearingService>(Reuse.Singleton);
        container.Register<INpcSenseService, NpcSenseService>(Reuse.Singleton);
        // After the script engine (70), as the mobile scripts.
        container.AddMoongateService<IItemScriptService, ItemScriptService>(LuaScriptEngineService.StartupPriority + 5);
        container.AddMoongateService<IGumpScriptService, GumpScriptService>(LuaScriptEngineService.StartupPriority + 5);
        container.AddMetricProvider<NpcTickMetricsProvider>();
        container.Register<ISectorService, SectorService>(Reuse.Singleton);
        container.Register<IMobileService, MobileService>(Reuse.Singleton);
        container.Register<IWorldViewService, WorldViewService>(Reuse.Singleton);
        container.Register<ITeleportService, TeleportService>(Reuse.Singleton);
        container.Register<IMoveOverService, MoveOverService>(Reuse.Singleton);
        container.Register<IWorldTransactionService, WorldTransactionService>(Reuse.Singleton);
        container.Register<ICharacterPresence, SessionCharacterPresence>(Reuse.Singleton);
        container.Register<ICharacterService, CharacterService>(Reuse.Singleton);
        container.AddScriptEvent<CharacterCreatedEvent>("character_created", CharacterScriptEvents.CharacterCreated);
        container.AddScriptEvent<CharacterDeletionRequestedEvent>(
            "character_deletion_requested",
            CharacterScriptEvents.CharacterDeletionRequested
        );
        container.AddScriptEvent<CharacterEnteredWorldEvent>(
            "character_entered_world",
            CharacterScriptEvents.CharacterEnteredWorld
        );
        container.AddScriptEvent<PlayerRegionChangedEvent>("player_region_changed", CharacterScriptEvents.PlayerRegionChanged);
        container.AddScriptEvent<PlayerSaidEvent>("player_say", CharacterScriptEvents.PlayerSay);
        container.AddScriptEvent<CharacterLeftWorldEvent>(
            "character_left_world",
            CharacterScriptEvents.CharacterLeftWorld
        );
        // Between the packet dispatcher (60), which closes the sessions, and the world save (40): stopping waits for
        // the leave saves before the final save and persistence shut down.
        container.AddMoongateService<CharacterLeaveWorldService>(50);
        container.RegisterMapping<ISessionClosedListener, CharacterLeaveWorldService>();
        container.RegisterMapping<ICharacterLeaveWorldService, CharacterLeaveWorldService>();
        container.Register<ICharacterEnterWorldService, CharacterEnterWorldService>(Reuse.Singleton);
        container.Register<ITargetService, TargetService>(Reuse.Singleton);
        container.RegisterMapping<ISessionClosedListener, ITargetService>();
        container.Register<IPromptService, PromptService>(Reuse.Singleton);
        container.RegisterMapping<ISessionClosedListener, IPromptService>();
        container.Register<IGumpService, GumpService>(Reuse.Singleton);
        container.RegisterMapping<ISessionClosedListener, IGumpService>();
        container.Register<IGumpTemplateService, GumpTemplateService>(Reuse.Singleton);
        container.Register<IBankService, BankService>(Reuse.Singleton);
        container.RegisterMapping<ISessionClosedListener, IBankService>();
        container.Register<IBroadcastService, BroadcastService>(Reuse.Singleton);
        container.Register<ISpeechService, SpeechService>(Reuse.Singleton);
        container.Register<IEffectService, EffectService>(Reuse.Singleton);
        container.Register<IPublicMoongateService, PublicMoongateService>(Reuse.Singleton);
        container.Register<ILocationService, LocationService>(Reuse.Singleton);
        container.Register<ITileDataService, TileDataService>(Reuse.Singleton);
        container.Register<IMovementService, MovementService>(Reuse.Singleton);
        container.Register<ILineOfSightService, LineOfSightService>(Reuse.Singleton);
        container.Register<IPathfindingService, PathfindingService>(Reuse.Singleton);
        container.Register<INpcPathService, NpcPathService>(Reuse.Singleton);
        container.AddUltimaScriptModules();

        // After IUltimaDataService (-10): loaders read MUL/UOP files after Files.SetDirectory.
        container.AddLiveWorldMobiles();
        container.AddLiveWorldItems();
        container.AddLiveWorldState();
        container.AddLiveJailSentences();

        container.AddMoongateService<IDataLoaderService, DataLoaderService>(-5);
        // After the loaders: the maps come from data/maps.toml.
        container.AddMoongateService<IMapService, MapService>(-4);
        // After IUltimaDataService: the multis come from the client directory.
        container.AddMoongateService<IMultiService, MultiService>(-4);
        // After the loaders and the item templates: checks the configured backpack and gold templates exist.
        container.AddMoongateService<IStartingItemsService, StartingItemsService>(-3);
        // After persistence is ready (it is, before any startup service): reserves item serials for the game loop.
        container.AddMoongateService<IItemSerialPool, ItemSerialPool>();
        // After the data loaders and the maps (the sector grid needs them), before the game server takes players and
        // the world save (40): the items on the ground are live before anyone can see them.
        container.AddMoongateService<IItemService, ItemService>(10);
        container.AddMoongateService<IWorldPropsService, WorldPropsService>(10);
        container.Register<IItemDecayQueue, ItemDecayQueue>(Reuse.Singleton);
        container.Register<IItemTimerQueue, ItemTimerQueue>(Reuse.Singleton);
        // After the item scripts are loaded: a timer that is due runs its script.
        container.AddMoongateService<IItemTimerService, ItemTimerService>(LuaScriptEngineService.StartupPriority + 6);
        // After the items (10): the ground items it deletes are loaded by then.
        container.AddMoongateService<ItemDecayService>(11);
        // The light cycle's timer, like the decay's: the players it lights come after the game server starts.
        container.AddMoongateService<ILightService, LightService>(11);
        // The weather follows the players' regions: RegionService tells it, as its region change listener.
        container.AddMoongateService<IWeatherService, WeatherService>(11);
        container.RegisterDelegate<IRegionChangeListener>(resolver => resolver.Resolve<IWeatherService>(), Reuse.Singleton);
        container.RegisterDelegate<IRegionChangeListener>(resolver => resolver.Resolve<ILightService>(), Reuse.Singleton);
        // The music follows the players' regions too.
        container.AddMoongateService<IMusicService, MusicService>(11);
        container.RegisterDelegate<IRegionChangeListener>(resolver => resolver.Resolve<IMusicService>(), Reuse.Singleton);
        // The seasons too; after a new season they send the light and the weather again. Keep it after the light and
        // the weather: the listeners run in this order, so those already follow the new region when it resends them.
        container.AddMoongateService<ISeasonService, SeasonService>(11);
        container.RegisterDelegate<IRegionChangeListener>(resolver => resolver.Resolve<ISeasonService>(), Reuse.Singleton);
        container.AddMoongateService<IRegionAnnouncer, RegionAnnouncer>(11);
        container.RegisterDelegate<IRegionChangeListener>(resolver => resolver.Resolve<IRegionAnnouncer>(), Reuse.Singleton);
        // Last: the scripts hear of the change once the light, the weather and the season of the place were sent.
        container.Register<IRegionChangeListener, RegionEventPublisher>(Reuse.Singleton);
        container.AddMoongateService<IEquipmentService, EquipmentService>();
        container.AddMoongateService<ITooltipService, TooltipService>();
        // As the ground items: the NPCs are live before the game server takes players.
        container.AddMoongateService<INpcService, NpcService>(10);
        container.Register<IItemSpawnService, ItemSpawnService>(Reuse.Singleton);
        container.Register<IMobileStateService, MobileStateService>(Reuse.Singleton);
        // After the NPCs (10), and stopped before the world save (40) stops the game loop, so no spawn is cut in half.
        container.AddMoongateService<ISpawnRegionService, SpawnRegionService>(50);

        return container;
    }
}
