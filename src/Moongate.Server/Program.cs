using ConsoleAppFramework;
using DryIoc;
using Moongate.Core.Types;
using Moongate.Server.Bootstrap;
using Moongate.Server.Data.Args;
using Serilog;

await ConsoleApp.RunAsync(
    args,
    async (
        CancellationToken cancellationToken, LogLevelType logLevel = LogLevelType.Information, bool logToFile = true,
        bool logPackets = false, string? rootDirectory = null
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

        container.RegisterInstance(serverArgs);

        Console.WriteLine($"Moongate Server starting with root directory: {serverArgs.RootDirectory}");
        Console.WriteLine($"Platform: {Environment.OSVersion.Platform}, Version: {Environment.OSVersion.Version}");
        Console.WriteLine($"Running on container: {isDocker}");

        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

        var bootstrap = new MoongateServerBootstrap(container, cancellationToken);

        await bootstrap.StartAsync();

        await bootstrap.RunAsync();

        await bootstrap.StopAsync();
    }
);
