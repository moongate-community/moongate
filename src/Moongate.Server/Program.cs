using ConsoleAppFramework;
using Moongate.Core.Json;
using Moongate.Core.Resources;
using Moongate.Core.Server.Bootstrap;
using Moongate.Core.Server.Data.Options;
using Moongate.Core.Server.Json;
using Moongate.Core.Server.Types;

JsonUtils.RegisterJsonContext(MoongateCoreServerContext.Default);

var cancellationTokenSource = new CancellationTokenSource();

await ConsoleApp.RunAsync(
    args,
    async (
        LogLevelType logLevel = LogLevelType.Debug,
        string rootDirectory = "",
        string ultimaDirectory = "",
        string configFile = "moongate.json"
    ) =>
    {
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;          // Prevent the process from terminating immediately
            cancellationTokenSource.Cancel(); // Signal cancellation
        };

        var header =ResourceUtils.GetEmbeddedResourceContent("Assets/header.txt", typeof(Program).Assembly);

        Console.WriteLine(header);


        var moongateArgsOptions = new MoongateArgsOptions
        {
            LogLevel = logLevel,
            ConfigFile = configFile,
            RootDirectory = rootDirectory,
            UltimaOnlineDirectory = ultimaDirectory
        };

        var bootstrap = new MoongateBootstrap(moongateArgsOptions, cancellationTokenSource.Token);

        bootstrap.Initialize();

        await bootstrap.StartAsync();
    }
);
