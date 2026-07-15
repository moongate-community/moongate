using ConsoleAppFramework;
using DryIoc;
using Moongate.Core.Interfaces;
using Moongate.Persistence;
using Moongate.Scripting;
using Moongate.Server;
using Moongate.Server.Autostart;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events;
using Moongate.Server.Data.Exceptions;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Accounts;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Mobiles;
using Moongate.Server.Interfaces.Network;
using Moongate.Server.Interfaces.World;
using Moongate.Server.Services.Accounts;
using Moongate.Server.Services.Game;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Mobiles;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.World;
using SquidStd.Abstractions.Extensions.Config;
using SquidStd.Abstractions.Extensions.Services;
using SquidStd.Core.Config;
using SquidStd.Core.Data.Bootstrap;
using SquidStd.Core.Extensions.Directories;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Extensions;
using SquidStd.Services.Core.Extensions;
using SquidStd.Services.Core.Services.Bootstrap;

const int loginHandoffTtlMs = 30_000;

await ConsoleApp.RunAsync(
    args,
    async (string rootDirectory = null, bool showHeader = true, string? uoDirectory = null, CancellationToken ct = default)
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

        stdBootstrap.UsePlugins(builder =>
            {
                builder.FromDirectory("plugins");
                builder.Add<MoongatePersistencePlugin>();
                builder.Add<MoongateScriptingPlugin>();
                builder.Add<MoongateScriptModulesPlugin>();
                builder.Add<MoongateDataLoaderPlugin>();
            }
        );

        stdBootstrap.ConfigureServices(container =>
            {
                // Binds the SAME cached instance mutated above; the file cannot clobber it.
                container.RegisterConfigSection<MoongateConfig>("moongate");

                container.Register<IAccountService, AccountService>(Reuse.Singleton);
                container.Register<ICharacterService, CharacterService>(Reuse.Singleton);
                container.Register<IMobileFactoryService, MobileFactoryService>(Reuse.Singleton);

                container.RegisterInstance(Random.Shared);
                container.RegisterInstance(TimeProvider.System);
                container.Register<IItemFactoryService, ItemFactoryService>(Reuse.Singleton);
                container.Register<IItemService, ItemService>(Reuse.Singleton);
                container.Register<ILootService, LootService>(Reuse.Singleton);
                container.Register<IWorldService, WorldService>(Reuse.Singleton);

                container.Register<TimerAutostartService>(Reuse.Singleton);

                container.RegisterInstance<IPendingLoginStore>(
                    new PendingLoginStore(loginHandoffTtlMs, () => Environment.TickCount64)
                );
                container.Register<ISessionManager, SessionManager>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, LoginSeedHandler>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, AccountLoginHandler>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, SelectServerHandler>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, GameServerLoginHandler>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, CharacterCreationHandler>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, CharacterSelectHandler>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, PingHandler>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, ClientVersionHandler>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, GeneralInformationHandler>(Reuse.Singleton);

                container.RegisterStdService<INetworkService, NetworkService>();

                container.RegisterMainThreadDispatcherService();
                container.RegisterTimerWheelService(new());
                container.Register<IGameLoopContext, GameLoopContext>(Reuse.Singleton);
                container.Register<ILoopThread, LoopThreadMarker>(Reuse.Singleton);
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

                var eventBus = container.Resolve<IEventBus>();

                eventBus.Subscribe<EngineStartedEvent>((_, _) =>
                    {
                        container.Resolve<TimerAutostartService>().InitDefaultTimers();

                        var loop = container.Resolve<IGameLoopContext>();
                        var marker = container.Resolve<ILoopThread>();

                        loop.Post(() =>
                            {
                                marker.Capture();
                                _ = eventBus.PublishAsync(new WorldReadyEvent());
                            }
                        );

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
