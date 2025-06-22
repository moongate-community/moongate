using System.Text.Json.Serialization;
using ConsoleAppFramework;
using DryIoc;
using Moongate.Core.Data.Configs.Services;
using Moongate.Core.Directories;
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
using Moongate.Server.Packets;
using Moongate.Server.Persistence;
using Moongate.Server.Services;
using Moongate.UO.Commands;
using Moongate.UO.Data;
using Moongate.UO.Data.Files;
using Moongate.UO.Data.Json.Converters;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Packets;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Packets.Login;
using Moongate.UO.Data.Packets.System;
using Moongate.UO.Data.Persistence;
using Moongate.Uo.Data.Types;
using Moongate.UO.Data.Types;
using Moongate.UO.Extensions;
using Moongate.UO.FileLoaders;
using Moongate.UO.Interfaces.Services;
using Moongate.UO.Modules;
using Moongate.UO.PacketHandlers;
using Serilog;

JsonUtils.RegisterJsonContext(MoongateCoreServerContext.Default);
JsonUtils.RegisterJsonContext(UOJsonContext.Default);

JsonUtils.AddJsonConverter(new JsonStringEnumConverter<Stat>());

JsonUtils.AddJsonConverter(new SerialConverter());
JsonUtils.AddJsonConverter(new RaceConverter());
JsonUtils.AddJsonConverter(new ProfessionInfoConverter());
JsonUtils.AddJsonConverter(new MapConverter());
JsonUtils.AddJsonConverter(new ClientVersionConverter());
JsonUtils.AddJsonConverter(new FlagsConverter<CharacterListFlags>());
JsonUtils.AddJsonConverter(new FlagsConverter<FeatureFlags>());
JsonUtils.AddJsonConverter(new FlagsConverter<HousingFlags>());
JsonUtils.AddJsonConverter(new FlagsConverter<MapSelectionFlags>());
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

        var header = ResourceUtils.GetEmbeddedResourceContent("Assets/_header.txt", typeof(Program).Assembly);


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
                .AddService(typeof(IMobileService), typeof(MobileService))
                .AddService(typeof(IFileLoaderService), typeof(FileLoaderService), -1)

                //
                .AddService(typeof(IEntityFileService), typeof(MoongateEntityFileService))
                .AddService(typeof(PacketLoggerService))
                ;

            container.AddService(typeof(AccountCommands));

            container.AddService(typeof(AfterLoginHandler));


            container.RegisterInstance<IEntityReader>(new MoongateEntityWriterReader());
            container.RegisterInstance<IEntityWriter>(new MoongateEntityWriterReader());
        };

        bootstrap.ConfigureScriptEngine += scriptEngine =>
        {
            scriptEngine.AddScriptModule(typeof(LoggerModule));
            scriptEngine.AddScriptModule(typeof(AccountModule));
        };

        bootstrap.ConfigureNetworkServices += networkService =>
        {
            PacketRegistration.RegisterPackets(networkService);


            // Registering all packet handlers

            networkService.RegisterGamePacketHandler<LoginRequestPacket, LoginHandler>();
            networkService.RegisterGamePacketHandler<LoginSeedPacket, LoginHandler>();
            networkService.RegisterGamePacketHandler<SelectServerPacket, LoginHandler>();
            networkService.RegisterGamePacketHandler<GameServerLoginPacket, LoginHandler>();
            networkService.RegisterGamePacketHandler<ClientVersionPacket, LoginHandler>();

            networkService.RegisterGamePacketHandler<CharacterCreationPacket, CharactersHandler>();
            networkService.RegisterGamePacketHandler<CharacterDeletePacket, CharactersHandler>();
            networkService.RegisterGamePacketHandler<CharacterLoginPacket, CharactersHandler>();


            networkService.RegisterGamePacketHandler<PingPacket, PingHandler>();

        };


        bootstrap.AfterInitialize += (container, config) =>
        {
            var fileLoaderService = container.Resolve<IFileLoaderService>();
            var directoriesConfig = container.Resolve<DirectoriesConfig>();
            CopyAssetsFilesAsync(directoriesConfig);

            UoFiles.ScanForFiles(config.UltimaOnlineDirectory);

            fileLoaderService.AddFileLoader<ClientVersionLoader>();
            fileLoaderService.AddFileLoader<SkillLoader>();
            fileLoaderService.AddFileLoader<ExpansionLoader>();
            fileLoaderService.AddFileLoader<BodyDataLoader>();
            fileLoaderService.AddFileLoader<ProfessionsLoader>();
            fileLoaderService.AddFileLoader<MultiDataLoader>();
            fileLoaderService.AddFileLoader<RaceLoader>();
            fileLoaderService.AddFileLoader<TileDataLoader>();
            fileLoaderService.AddFileLoader<MapLoader>();
        };

        bootstrap.Initialize();

        await bootstrap.StartAsync();
    }
);

static async Task CopyAssetsFilesAsync(DirectoriesConfig directoriesConfig)
{
    var assets = ResourceUtils.GetEmbeddedResourceNames(typeof(Program).Assembly, "Assets");
    var files = assets.Select(s => new
            { Asset = s, FileName = ResourceUtils.ConvertResourceNameToPath(s, "Moongate.Server.Assets") }
        )
        .ToList();


    foreach (var assetFile in files)
    {
        var fileName = Path.Combine(directoriesConfig.Root, assetFile.FileName);

        if (assetFile.FileName.StartsWith("_"))
        {
            continue;
        }

        if (!File.Exists(fileName))
        {
            Log.Logger.Information("Copying asset {FileName}", fileName);

            var content = ResourceUtils.GetEmbeddedResourceContent(assetFile.Asset, typeof(Program).Assembly);

            var directory = Path.GetDirectoryName(fileName);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fileName, content);
        }
    }
}
