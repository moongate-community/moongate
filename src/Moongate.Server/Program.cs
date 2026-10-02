using ConsoleAppFramework;
using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Extensions.Directories;
using Moongate.Core.Types;
using Moongate.Core.Utils;
using Moongate.Persistence.Extensions;
using Moongate.Server.Admin;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Bootstrap.Internal.Setup;
using Moongate.Server.Commands;
using Moongate.Server.Core.Data.Args;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Helpers;
using Moongate.Server.Services.Commands;
using Moongate.Server.Services.Console;
using Moongate.Server.Services.Logging;
using Moongate.Server.Services.Diagnostics;
using Moongate.Server.Services.Diagnostics.Providers;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.Plugins;
using Moongate.Server.Types.Persistence;
using Moongate.Server.Ultima;
using Serilog;

await ConsoleApp.RunAsync(
    args,
    async (
        CancellationToken cancellationToken, LogLevelType logLevel = LogLevelType.Information, bool logToFile = true,
        bool logPackets = false, string? rootDirectory = null, bool showHeader = true, string pidFileName = "moongate.pid",
        PersistenceSchemaMode persistenceSchema = PersistenceSchemaMode.None,
        string? migrationOutput = null, string? migrationTarget = null, bool initializeRoot = false,
        bool generateAdminCertificate = false, string? adminCertificateHosts = null
    ) =>
    {
        rootDirectory ??= Environment.GetEnvironmentVariable("MOONGATE_ROOT") ?? AppContext.BaseDirectory;

        rootDirectory = rootDirectory.ResolvePathAndEnvs();

        try
        {
            RootDirectoryGuard.EnsureNotBinaryDirectory(rootDirectory, AppContext.BaseDirectory);
        }
        catch (InvalidOperationException exception)
        {
            await Console.Error.WriteLineAsync($"Moongate startup aborted: {exception.Message}");
            Environment.ExitCode = 2;

            return;
        }

        if (generateAdminCertificate && !initializeRoot || adminCertificateHosts is not null && !generateAdminCertificate)
        {
            await Console.Error.WriteLineAsync(
                "Certificate options require --initialize-root and --generate-admin-certificate."
            );
            Environment.ExitCode = 2;

            return;
        }

        if (initializeRoot)
        {
            try
            {
                RootDirectoryInitializer.Initialize(
                    rootDirectory,
                    Path.Combine(AppContext.BaseDirectory, "migrations"),
                    Path.Combine(AppContext.BaseDirectory, "data"),
                    Console.Out,
                    generateAdminCertificate ? adminCertificateHosts?.Split(',') ?? [] : null,
                    Path.Combine(AppContext.BaseDirectory, "templates"),
                    Path.Combine(AppContext.BaseDirectory, "scripts")
                );
            }
            catch (Exception exception)
            {
                await Console.Error.WriteLineAsync($"Root initialization failed: {exception.Message}");
                Environment.ExitCode = 1;
            }

            return;
        }

        if (persistenceSchema != PersistenceSchemaMode.None)
        {
            try
            {
                await PersistenceSchemaCommand.ExecuteAsync(
                    rootDirectory,
                    persistenceSchema,
                    Console.Out,
                    cancellationToken,
                    migrationOutput,
                    migrationTarget
                );
            }
            catch (Exception exception)
            {
                await Console.Error.WriteLineAsync($"Persistence schema command failed: {exception.Message}");
                Environment.ExitCode = 1;
            }

            return;
        }

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

        var directoriesConfig = new DirectoriesConfig(rootDirectory, ["logs", "plugins", "config", "scripts"]);

        var serverArgs = new MoongateServerArgs
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

        // An exception shows its message on the console and gets a report to paste into a GitHub issue.
        Log.Logger = ServerLoggerFactory.Create(
            consolePrompt,
            directoriesConfig["logs"],
            logToFile,
            VersionUtils.GetVersion(typeof(Program).Assembly),
            VersionUtils.GetCodename(typeof(Program).Assembly)
        );

        var configPath = Path.Combine(directoriesConfig["config"], "moongate.toml");
        var serverConfig = ConfigHelper.Load(configPath);
        var configDocument = ConfigHelper.ReadDocument(configPath);
        Log.Information("Server mode: {ServerMode}", serverConfig.Mode);
        Log.Information("Client encryption: {Encryption}", serverConfig.Network.Encryption.GetDescription());

        var bootstrap = new MoongateServerBootstrap(container, cancellationToken)
            .RegisterServices(services =>
                {
                    services.RegisterInstance(directoriesConfig);
                    services.RegisterInstance(serverArgs);
                    services.RegisterInstance(serverConfig);
                    services.RegisterInstance(configDocument);
                    services.RegisterInstance(serverConfig.Diagnostics.ToOptions());
                    services.RegisterInstance(TimeProvider.System);
                    services.RegisterMoongatePersistence(
                            serverConfig.Persistence.ToOptions(
                                Path.Combine(directoriesConfig.Root, "migrations"),
                                directoriesConfig["plugins"],
                                serverConfig.Mode,
                                rootDirectory
                            )
                        )
                        .AddMoongateService<IEventBusService, EventBusService>();

                    services.AddMetricProvider<SystemMetricsProvider>();
                    services.AddMoongateService<IDiagnosticService, DiagnosticService>(DiagnosticService.StartupPriority)
                        .AddMoongateService<IPluginLoaderService, PluginLoaderService>(() =>
                            new(services, directoriesConfig)
                        )
                        .AddMoongateService<ICommandSystemService, CommandSystemService>()
                        .RegisterCommand<EchoCommand>(
                            "echo|e",
                            "Echoes back its arguments.",
                            CommandSourceType.Console | CommandSourceType.InGame,
                            AccountType.Regular,
                            CommandMessages.EchoDescription
                        )
                        .RegisterCommand<HelpCommand>(
                            "help",
                            "Lists available commands or shows details for one command.",
                            CommandSourceType.Console | CommandSourceType.InGame,
                            AccountType.Regular,
                            CommandMessages.HelpDescriptionText
                        )
                        .RegisterCommand<ConsoleCommand>(
                            "console",
                            "Locks the console input again, as at startup: console lock.",
                            descriptionMessage: CommandMessages.ConsoleDescription
                        )
                        .AddMoongateService<IConsolePromptService>(consolePrompt)
                        .AddMoongateService<IConsoleInputService, ConsoleInputService>(1000);

                    ServerRoleRegistration.Register(services, serverConfig, directoriesConfig);
                    container.RegisterPlugin<MoongateUltimaPlugin>()
                        .RegisterPlugin<MoongateAdminPlugin>();

                    return services;
                }
            );

        await MoongateServerRunner.RunAsync(bootstrap);
    }
);
