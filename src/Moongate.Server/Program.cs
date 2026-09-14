using ConsoleAppFramework;
using DryIoc;
using Moongate.Core.Types;
using Moongate.Core.Utils;
using Moongate.Persistence.Extensions;
using Moongate.Server.Bootstrap;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Data.Args;
using Moongate.Server.Services.Persistence;
using Serilog;

await ConsoleApp.RunAsync(
    args,
    async (
        CancellationToken cancellationToken, LogLevelType logLevel = LogLevelType.Information, bool logToFile = true,
        bool logPackets = false, string? rootDirectory = null, bool showHeader = true
    ) =>
    {
        rootDirectory ??= Environment.GetEnvironmentVariable("MOONGATE_ROOT") ?? AppContext.BaseDirectory;

        var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
        var container = new Container();

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

            Console.WriteLine(headerContent);
        }

        container.RegisterInstance(serverArgs);
        container.RegisterMoongatePersistence(Path.Combine(serverArgs.RootDirectory, "save"))
            .RegisterMoongateService<MoongatePersistenceStartupService>(
                MoongatePersistenceStartupService.StartupPriority
            );

        Console.WriteLine($"Moongate Server starting with root directory: {serverArgs.RootDirectory}");
        Console.WriteLine($"Platform: {Environment.OSVersion.Platform}, Version: {Environment.OSVersion.Version}");
        Console.WriteLine($"Running on container: {isDocker}");

        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

        var bootstrap = new MoongateServerBootstrap(container, cancellationToken);

        await MoongateServerRunner.RunAsync(bootstrap);
    }
);
