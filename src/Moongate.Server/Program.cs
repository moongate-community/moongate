using ConsoleAppFramework;
using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Extensions.Directories;
using Moongate.Core.Types;
using Moongate.Core.Utils;
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Persistence.Extensions;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Modules;
using Moongate.Scripting.Services;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Commands;
using Moongate.Server.Core.Data.Args;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Handlers.General;
using Moongate.Server.Handlers.Login;
using Moongate.Server.Helpers;
using Moongate.Server.Services.Commands;
using Moongate.Server.Services.Console;
using Moongate.Server.Services.Console.Internal.Logging;
using Moongate.Server.Services.Diagnostics;
using Moongate.Server.Services.Diagnostics.Providers;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Services.Timing;
using Moongate.Server.Services.Persistence.Internal;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Plugins;
using Moongate.Server.Services.Ultima;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Templates;
using Serilog.Templates.Themes;

await ConsoleApp.RunAsync(
    args,
    async (
        CancellationToken cancellationToken, LogLevelType logLevel = LogLevelType.Information, bool logToFile = true,
        bool logPackets = false, string? rootDirectory = null, bool showHeader = true, string pidFileName = "moongate.pid"
    ) =>
    {
        rootDirectory ??= Environment.GetEnvironmentVariable("MOONGATE_ROOT") ?? AppContext.BaseDirectory;

        rootDirectory = rootDirectory.ResolvePathAndEnvs();

        PidFileGuard processGuard;

        try
        {
            processGuard = PidFileGuard.Acquire(rootDirectory, pidFileName);
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                                       IOException or
                                                       UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync($"Moongate startup aborted: {exception.Message}");
            Environment.ExitCode = 1;

            return;
        }

        using var pidFileGuard = processGuard;

        var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
        var container = new Container();

        var directoriesConfig = new DirectoriesConfig(rootDirectory, ["logs", "plugins", "config", "save", "scripts"]);

        var serverArgs = new MoongateServerArgs()
        {
            LogLevel = logLevel,
            LogPackets = logPackets,
            LogToFile = logToFile,
            RootDirectory = rootDirectory ?? AppContext.BaseDirectory
        };

        if (showHeader)
        {
            var headerContent = ResourceUtils.GetEmbeddedResourceString(typeof(Program).Assembly, "Assets/header.txt");

            headerContent = headerContent.Replace("{Version}", VersionUtils.GetVersion(typeof(Program).Assembly));
            headerContent = headerContent.Replace("{Codename}", VersionUtils.GetCodename(typeof(Program).Assembly));

            Console.WriteLine(headerContent);
        }

        Console.WriteLine($"Moongate Server starting with root directory: {serverArgs.RootDirectory}");
        Console.WriteLine($"Platform: {Environment.OSVersion.Platform}, Version: {Environment.OSVersion.Version}");
        Console.WriteLine($"Running on container: {isDocker}");

        var consolePrompt = new ConsolePromptService();

        var consoleLogger = new LoggerConfiguration()

                            // pass-through: the outer logger owns level policy
                            .MinimumLevel
                            .Verbose()
                            .WriteTo
                            .Console(
                                new ExpressionTemplate(
                                    "{@t:HH:mm:ss.fff} {@l:u3} " +
                                    "{Coalesce(Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1), 'Moongate'),-28}" +
                                    " | {@m}\n{@x}",
                                    theme: TemplateTheme.Code
                                )
                            )
                            .CreateLogger();

        var loggingConfiguration = new LoggerConfiguration()
                                   .WriteTo
                                   .Sink(new PromptAwareConsoleSink(consolePrompt, consoleLogger));

        if (logToFile)
        {
            // .clef is the conventional extension for this format, and log shippers
            // recognise it without being told what the file holds.
            var logFilePath = Path.Combine(directoriesConfig["logs"], "moongate-.clef");

            loggingConfiguration = loggingConfiguration.WriteTo.File(
                // One JSON object per line, keeping the message template and its
                // properties separate so a log reader can group events by template.
                new CompactJsonFormatter(),
                logFilePath,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB
                retainedFileCountLimit: 30
            );
        }

        Log.Logger = loggingConfiguration.CreateLogger();

        var serverConfig = ConfigHelper.Load(Path.Combine(directoriesConfig["config"], "moongate.toml"));

        var bootstrap = new MoongateServerBootstrap(container, cancellationToken)
            .RegisterServices(
                services =>
                {
                    services.RegisterInstance(directoriesConfig);
                    services.RegisterInstance(serverArgs);
                    services.RegisterInstance(serverConfig);
                    services.RegisterInstance(serverConfig.WorldSave.ToOptions());
                    services.RegisterInstance(serverConfig.Scripting.ToOptions(directoriesConfig["scripts"]));
                    services.RegisterInstance(serverConfig.Diagnostics.ToOptions());
                    services.RegisterInstance(new GameLoopOptions());
                    services.RegisterInstance<TimeProvider>(TimeProvider.System);
                    services.RegisterInstance(new TimerWheelOptions());
                    services.RegisterDelegate<ITimerService>(
                        resolver => resolver.Resolve<TimerWheelService>(),
                        Reuse.Singleton
                    );

                    services.RegisterMoongatePersistence(directoriesConfig["save"])
                            .RegisterMoongateService<MoongatePersistenceStartupService>(
                                MoongatePersistenceStartupService.StartupPriority
                            )
                            .RegisterMoongateService<TimerWheelService>(priority: -900)
                            .RegisterMoongateService<IGameLoopService, GameLoopService>(priority: -800)
                            .RegisterMoongateService<IUltimaDataService, UltimaDataService>(-10)
                            .RegisterMoongateService<IWorldSaveService, WorldSaveService>(WorldSaveService.StartupPriority)
                            .RegisterMoongateService<ISessionService, SessionService>()
                            .RegisterMoongateService<IEventBusService, EventBusService>();

                    services.AddMetricProvider<SystemMetricsProvider>()
                            .AddMetricProvider<GameLoopMetricsProvider>()
                            .AddMetricProvider<TimerMetricsProvider>()
                            .AddMetricProvider<SessionMetricsProvider>();
                    services.RegisterMoongateService<IDiagnosticService, DiagnosticService>(
                                DiagnosticService.StartupPriority
                            )
                            .RegisterMoongateService<IPluginLoaderService, PluginLoaderService>(
                                () => new PluginLoaderService(services, directoriesConfig)
                            )
                            .RegisterPacketHandler<PingPacket, PingPacketHandler>()
                            .RegisterPacketHandler<ClientVersionPacket, ClientVersionPacketHandler>()
                            .RegisterMoongateService<ICommandSystemService, CommandSystemService>()
                            .RegisterCommand<EchoCommand>(
                                "echo|e",
                                "Echoes back its arguments.",
                                CommandSourceType.Console | CommandSourceType.InGame,
                                AccountType.Regular
                            )
                            .RegisterMoongateService<IScriptEngine, LuaScriptEngineService>(LuaScriptEngineService.StartupPriority)
                            .RegisterScriptModule<LogModule>()
                            .RegisterCommand<ScriptCommand>(
                                "script",
                                "Reloads a script file or prints the engine's counters: script reload <file> | script metrics.",
                                CommandSourceType.Console,
                                AccountType.Administrator
                            )
                            .RegisterMoongateService<IConsolePromptService>(consolePrompt)
                            .RegisterMoongateService<IConsoleInputService, ConsoleInputService>(priority: 1000);

                    PacketPipelineRegistration.Register(services);

                    return services;
                }
            );

        await MoongateServerRunner.RunAsync(bootstrap);
    }
);
