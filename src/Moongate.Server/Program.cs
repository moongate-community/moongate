using ConsoleAppFramework;
using DryIoc;
using Moongate.Core.Interfaces;
using Moongate.Persistence;
using Moongate.Scripting;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Exceptions;
using Moongate.Server.Handlers;
using Moongate.Server.Extensions;
using Moongate.Server.Interfaces;
using Moongate.Server.Loaders;
using Moongate.Server.Services;
using Moongate.Server.Services.Network;
using SquidStd.Abstractions.Extensions.Config;
using SquidStd.Abstractions.Extensions.Services;
using SquidStd.Core.Config;
using SquidStd.Core.Data.EventLoop;
using SquidStd.Core.Data.Jobs;
using SquidStd.Core.Data.Timing;
using SquidStd.Core.Extensions.Directories;
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

        stdBootstrap.UsePlugins(
            builder =>
            {
                builder.FromDirectory("plugins");
                builder.Add<MoongatePersistencePlugin>();
                builder.Add<MoongateScriptingPlugin>();
            }
        );

        stdBootstrap.ConfigureServices(
            container =>
            {
                // Binds the SAME cached instance mutated above; the file cannot clobber it.
                container.RegisterConfigSection<MoongateConfig>("moongate");

                container.Register<IAccountService, AccountService>(Reuse.Singleton);
                container.RegisterInstance<IPendingLoginStore>(
                    new PendingLoginStore(loginHandoffTtlMs, () => Environment.TickCount64)
                );
                container.Register<ISessionManager, SessionManager>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, LoginSeedHandler>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, AccountLoginHandler>(Reuse.Singleton);
                container.Register<IPacketHandlerRegistration, SelectServerHandler>(Reuse.Singleton);

                container.RegisterStdService<INetworkService, NetworkService>();

                // Priority 100 so it starts after the event bus and the Lua forwarder are up,
                // ensuring subscribers actually receive the FilesLoadedEvent.
                container.RegisterStdService<FilesLoaderService, FilesLoaderService>(100);

                container.Register<ISkillService, SkillService>(Reuse.Singleton);
                container.RegisterDataLoader<SkillLoader>();
                container.RegisterDataLoaderService();

                container.RegisterMainThreadDispatcherService();
                container.RegisterTimerWheelService(
                    new TimerWheelConfig()
                        { }
                );
                container.Register<IGameLoopContext, GameLoopContext>(Reuse.Singleton);
                container.RegisterEventLoop(
                    new EventLoopConfig()
                    {
                        IdleSleepMs = 1,
                        IdleCpuEnabled = true,
                        SlowTickThresholdMs = 250
                    }
                );

                container.RegisterJobSystemService(
                    new JobsConfig()
                    {
                        ShutdownTimeoutSeconds = 5,
                        WorkerThreadCount = Environment.ProcessorCount - 1,
                    }
                );

                container.RegisterEventBusService();

                return container;
            }
        );

        // The UO client directory is loaded by FilesLoaderService at startup, which then
        // publishes FilesLoadedEvent on the event bus.
        await stdBootstrap.RunAsync(ct);
    }
);
