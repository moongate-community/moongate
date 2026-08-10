using ConsoleAppFramework;
using DryIoc;
using Moongate.Console.Admin.Plugin;
using Moongate.Core.Interfaces;
using Moongate.Http.Plugin;
using Moongate.News.Plugin;
using Moongate.Persistence;
using Moongate.Scripting;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Extensions;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Interfaces.Gumps;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Abstractions.Interfaces.Network;
using Moongate.Server.Abstractions.Interfaces.Notifications;
using Moongate.Server.Abstractions.Interfaces.Plugins;
using Moongate.Server.Abstractions.Interfaces.Server;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Autostart;
using Moongate.Server.Data.Exceptions;
using Moongate.Server.Extensions;
using Moongate.Server.Plugins;
using Moongate.Server.Services.Accounts;
using Moongate.Server.Services.AI;
using Moongate.Server.Services.Chat;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.Game;
using Moongate.Server.Services.Gumps;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Mobiles;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Notifications;
using Moongate.Server.Services.Notifications.Channels;
using Moongate.Server.Services.Plugins;
using Moongate.Server.Services.Server;
using Moongate.Server.Services.World;
using Moongate.Smtp.Plugin;
using Serilog;
using SquidStd.Abstractions.Extensions.Config;
using SquidStd.Abstractions.Extensions.Services;
using SquidStd.Core.Config;
using SquidStd.Core.Data.Bootstrap;
using SquidStd.Core.Extensions.Directories;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Core.Directories;
using Moongate.Server.Abstractions.Types;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;
using SquidStd.Plugin.Extensions;
using SquidStd.Services.Core.Extensions;
using SquidStd.Services.Core.Services.Bootstrap;

const int loginHandoffTtlMs = 30_000;

await ConsoleApp.RunAsync(
    args,
    async (
            string rootDirectory = null,
            bool showHeader = true,
            string? uoDirectory = null,
            bool disableWebPlugin = false,
            CancellationToken ct = default
        )
        =>
    {
        rootDirectory ??= Environment.GetEnvironmentVariable("MOONGATE_ROOT");

        if (string.IsNullOrEmpty(rootDirectory))
        {
            rootDirectory = (rootDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "moongate_root"))
                .ResolvePathAndEnvs();
        }
        else
        {
            rootDirectory = rootDirectory.ResolvePathAndEnvs();
        }

        uoDirectory = (uoDirectory ?? "~/uo").ResolvePathAndEnvs();

        if (showHeader)
        {
            var headerFile = ResourceUtils.GetEmbeddedResourceString(typeof(Program).Assembly, "Assets/header.txt");

            Console.WriteLine(headerFile);
        }

        // Config-first: the YAML is loaded eagerly, outside the container, and the sections
        // are real objects from here on. CLI overrides happen BEFORE anything consumes them.
        var config = SquidStdConfig.Load("moongate", rootDirectory);
        var moongateConfig = config.GetSection<MoongateConfig>("moongate");

        if (!string.IsNullOrEmpty(uoDirectory))
        {
            moongateConfig.UltimaDirectory = uoDirectory;
        }

        NpcAiConfigValidator.Validate(moongateConfig.NpcAi);

        // Set before any facet is touched: a TileMatrix reads this at construction, and the first
        // block query is what constructs it.
        Moongate.Ultima.Io.Files.CacheCapacityMapBlocks = Math.Max(moongateConfig.MapBlockCacheSize, 0);

        if (string.IsNullOrEmpty(moongateConfig.UltimaDirectory))
        {
            throw new UODirectoryNotValidException(
                "UltimaDirectory is not set in the config; clients will not be able to connect."
            );
        }

        var stdBootstrap = SquidStdBootstrap.Create(
            config,
            new()
            {
                AppName = "Moongate",
                AppVersion = VersionUtils.GetVersion(typeof(Program).Assembly),
                ConfigName = "moongate",
                RootDirectory = rootDirectory
            }
        );

        // Serilog first (idempotent): plugin-load logs and any pre-start logging become visible.
        // Safe with config-first: sections bind eagerly at registration, even after this call.
        stdBootstrap.ConfigureLogging();

        // Built before the plugins are added and registered after, because the two halves happen in
        // different bootstrap phases: activation here, container registration in ConfigureServices below.
        var pluginCatalog = new PluginCatalog();

        stdBootstrap.UsePlugins(
            builder =>
            {
                builder.FromDirectory("plugins");

                builder.AddTracked<MoongatePersistencePlugin>(pluginCatalog)
                       .AddTracked<MoongateScriptingPlugin>(pluginCatalog)
                       .AddTracked<MoongateScriptModulesPlugin>(pluginCatalog)
                       .AddTracked<MoongateNpcAiPlugin>(pluginCatalog)
                       .AddTracked<MoongateDataLoaderPlugin>(pluginCatalog)
                       .AddTracked<MoongateCommandsPlugin>(pluginCatalog)
                       .AddTracked<MoongatePacketHandlersPlugin>(pluginCatalog)
                       .AddTracked<MoongateEventSubscribersPlugin>(pluginCatalog);

                if (!disableWebPlugin)
                {
                    builder.AddTracked<MoongateHttpPlugin>(pluginCatalog);
                }
                else
                {
                    Log.Logger.Warning("HTTP is disabled");
                }

                builder.AddTracked<MoongateConsolePlugin>(pluginCatalog)
                       .AddTracked<MoongateNewsPlugin>(pluginCatalog)
                       .AddTracked<MoongateSmtpPlugin>(pluginCatalog);
            }
        );

        stdBootstrap.ConfigureServices(
            container =>
            {
                // Binds the SAME cached instance mutated above; the file cannot clobber it.
                container.RegisterConfigSection<MoongateConfig>("moongate");
                container.RegisterConfigSection<NotificationConfig>("notifications");

                // FromDirectory loads whatever it finds but hands back no list of it, so those plugins are
                // recovered by elimination. Swept here rather than inside UsePlugins above because by now
                // every plugin has certainly been loaded, and recorded after the explicit ones so that a
                // duplicate id loses.
                foreach (var type in PluginDiscovery.ExternalPluginTypes(
                             typeof(Program).Assembly,
                             AppDomain.CurrentDomain.GetAssemblies()
                         ))
                {
                    if (Activator.CreateInstance(type) is ISquidStdPlugin external)
                    {
                        pluginCatalog.Record(external, true);
                    }
                }

                // The instance the plugin phase filled in, not a fresh one.
                container.RegisterInstance<IPluginCatalog>(pluginCatalog);

                container.Register<IAccountService, AccountService>(Reuse.Singleton);
                container.Register<ICharacterService, CharacterService>(Reuse.Singleton);
                container.Register<ICharacterQueryService, CharacterQueryService>(Reuse.Singleton);
                container.Register<IMobileFactoryService, MobileFactoryService>(Reuse.Singleton);
                container.Register<IMobileService, MobileService>(Reuse.Singleton);

                container.RegisterInstance(Random.Shared);
                container.RegisterInstance(TimeProvider.System);
                container.Register<IItemFactoryService, ItemFactoryService>(Reuse.Singleton);
                container.Register<IItemService, ItemService>(Reuse.Singleton);
                container.Register<IStackableRule, TileDataStackableRule>(Reuse.Singleton);
                container.Register<IContainerRule, TileDataContainerRule>(Reuse.Singleton);
                container.Register<IDragDropService, DragDropService>(Reuse.Singleton);
                container.Register<IContainerOpenerRegistry, ContainerOpenerRegistry>(Reuse.Singleton);
                container.Register<ILootService, LootService>(Reuse.Singleton);
                container.Register<IVirtualSerialService, VirtualSerialService>(Reuse.Singleton);
                container.Register<ILightService, LightService>(Reuse.Singleton);
                container.Register<IWorldService, WorldService>(Reuse.Singleton);
                container.Register<IChatService, ChatService>(Reuse.Singleton);

                // The message of the day needs the runtime root, the build it is running, and a way
                // to count who is in the world -- none of which a constructor can be handed by name.
                container.RegisterDelegate<IMotdService>(
                    resolver => new MotdService(
                        resolver.Resolve<DirectoriesConfig>(),
                        VersionUtils.GetVersion(typeof(Program).Assembly),
                        () => resolver.Resolve<ISessionManager>()
                                      .All.Count(
                                          session => session.State == SessionStateType.InWorld &&
                                                     session.Character is not null
                                      )
                    ),
                    Reuse.Singleton
                );
                container.Register<IVisibilityService, VisibilityService>(Reuse.Singleton);
                container.Register<IPlayerTargetService, PlayerTargetService>(Reuse.Singleton);
                container.Register<IGumpService, GumpService>(Reuse.Singleton);
                container.Register<IServerSettingsService, ServerSettingsService>(Reuse.Singleton);

                // RegisterStdService rather than Register: the type is both the domain service the stats
                // endpoint resolves and the hosted service owning the refresh timer, and it must be the
                // same singleton in both roles.
                container.RegisterStdService<IServerStatsService, ServerStatsService>();
                container.RegisterStdService<LightCycleService, LightCycleService>();
                container.RegisterStdService<VisibilityReconciler, VisibilityReconciler>();

                // INotificationTemplateService is registered by the data-loader plugin, alongside the
                // loader that fills it, the same way the template services are.
                container.Register<INotificationService, NotificationService>(Reuse.Singleton);
                container.RegisterNotificationChannel<LogNotificationChannel>();
                container.RegisterCommandService();
                container.Register<IUltimaMapProvider, UltimaMapProvider>(Reuse.Singleton);
                container.Register<IMapTileService, MapTileService>(Reuse.Singleton);
                container.Register<IMovementService, MovementService>(Reuse.Singleton);
                container.RegisterStdService<MovementQueueService, MovementQueueService>();
                container.Register<ISpatialIndexService, SpatialIndexService>(Reuse.Singleton);
                container.Register<IOplService, OplService>(Reuse.Singleton);
                container.Register<DecorationPlacementService>(Reuse.Singleton);

                container.Register<TimerAutostartService>(Reuse.Singleton);

                container.RegisterInstance<IPendingLoginStore>(
                    new PendingLoginStore(loginHandoffTtlMs, () => Environment.TickCount64)
                );
                container.Register<ISessionManager, SessionManager>(Reuse.Singleton);

                container.RegisterStdService<INetworkService, NetworkService>();

                container.RegisterMainThreadDispatcherService();
                container.RegisterTimerWheelService(new());
                container.Register<IGameLoopContext, GameLoopContext>(Reuse.Singleton);
                container.Register<ILoopThread, EventLoopThread>(Reuse.Singleton);
                container.Register<ILoopAffinity, LoopAffinity>(Reuse.Singleton);
                container.RegisterEventLoop(
                    new()
                    {
                        IdleSleepMs = 1,
                        IdleCpuEnabled = true,
                        SlowTickThresholdMs = 250
                    }
                );

                container.RegisterJobSystemService(
                    new()
                    {
                        ShutdownTimeoutSeconds = 5,
                        WorkerThreadCount = Environment.ProcessorCount - 1
                    }
                );

                container.RegisterEventBusService();

                // Decorate the bus so loop-affine events route onto the game loop structurally,
                // instead of each publisher remembering to marshal by hand.
                container.Register<IEventBus, LoopAffineEventBus>(setup: Setup.Decorator);

                var eventBus = container.Resolve<IEventBus>();

                eventBus.Subscribe<EngineStartedEvent>(
                    (_, _) =>
                    {
                        container.Resolve<TimerAutostartService>().InitDefaultTimers();

                        // Resolved at startup so motd.txt is on disk for the operator to find and
                        // edit. Left to the first greeting, it would appear only once somebody had
                        // logged in -- by which point they had already been greeted without it.
                        container.Resolve<IMotdService>().Lines();

                        var loop = container.Resolve<IGameLoopContext>();

                        // WorldReadyEvent must fire on the loop, once the world is loaded.
                        loop.Post(() => _ = eventBus.PublishAsync(new WorldReadyEvent()));

                        return Task.CompletedTask;
                    }
                );

                return container;
            }
        );

        // The UO client directory is loaded by FilesLoaderService at startup, which then
        // publishes FilesLoadedEvent on the event bus.
        await stdBootstrap.RunAsync(ct);
    }
);
