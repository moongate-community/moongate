using ConsoleAppFramework;
using DryIoc;
using Moongate.Core.Types;
using Moongate.Server.Data.Args;
using Serilog;

await ConsoleApp.RunAsync(
    args,
    (
        CancellationToken cancellationToken, LogLevelType logLevel = LogLevelType.Information, bool logToFile = true,
        bool logPackets = false, string? rootDirectory = null
    ) =>
    {
        rootDirectory ??= Environment.GetEnvironmentVariable("MOONGATE_ROOT") ?? AppContext.BaseDirectory;

        var container = new Container();

        var serverArgs = new MoongateServerArgs()
        {
            LogLevel = logLevel,
            LogPackets = logPackets,
            LogToFile = logToFile,
            RootDirectory = rootDirectory ?? AppContext.BaseDirectory
        };

        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

        Log.Logger.Information("Starting up");
        Log.Logger.Error("Error");
    }
);
