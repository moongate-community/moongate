using ConsoleAppFramework;
using DryIoc;
using Moongate.Core.Data.Configs.Services;
using Moongate.Core.Json;
using Moongate.Core.Persistence.Interfaces.Entities;
using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Persistence.Services;
using Moongate.Core.Resources;
using Moongate.Core.Server.Bootstrap;
using Moongate.Core.Server.Data.Options;
using Moongate.Core.Server.Extensions;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Json;
using Moongate.Core.Server.Types;
using Moongate.Server.Loggers;
using Moongate.Server.Modules;
using Moongate.Server.Persistence;
using Moongate.Server.Services;
using Moongate.UO.Data.Persistence;
using Moongate.UO.Interfaces;
using Moongate.UO.Interfaces.Services;

JsonUtils.RegisterJsonContext(MoongateCoreServerContext.Default);
JsonUtils.RegisterJsonContext(UOJsonContext.Default);

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

        var header = ResourceUtils.GetEmbeddedResourceContent("Assets/header.txt", typeof(Program).Assembly);

        Console.WriteLine(header);


        var moongateArgsOptions = new MoongateArgsOptions
        {
            LogLevel = logLevel,
            ConfigFile = configFile,
            RootDirectory = rootDirectory,
            UltimaOnlineDirectory = ultimaDirectory
        };

        var bootstrap = new MoongateBootstrap(moongateArgsOptions, cancellationTokenSource);

        bootstrap.ConfigureServices += container =>
        {
            container.RegisterInstance(new ScriptEngineConfig());

            container
                .AddService(typeof(IVersionService), typeof(VersionService))
                .AddService(typeof(IScriptEngineService), typeof(JsScriptEngineService))
                .AddService(typeof(IGameSessionService), typeof(GameSessionService))
                .AddService(typeof(INetworkService), typeof(NetworkService))
                .AddService(typeof(ICommandSystemService), typeof(CommandSystemService))
                .AddService(typeof(IAccountService), typeof(AccountService))
                //
                .AddService(typeof(IEntityFileService), typeof(MoongateEntityFileService))
                .AddService(typeof(PacketLoggerService))
                ;


            container.RegisterInstance<IEntityReader>(new MoongateEntityWriterReader());
            container.RegisterInstance<IEntityWriter>(new MoongateEntityWriterReader());
        };

        bootstrap.ConfigureScriptEngine += scriptEngine => { scriptEngine.AddScriptModule(typeof(LoggerModule)); };

        bootstrap.Initialize();

        await bootstrap.StartAsync();
    }
);
