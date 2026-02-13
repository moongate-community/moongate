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
using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Json;
using Moongate.Core.Server.Types;
using Moongate.Server.Commands;
using Moongate.Server.Generator;
using Moongate.Server.Loggers;
using Moongate.Server.Modules;
using Moongate.Server.Packets;
using Moongate.Server.Persistence;
using Moongate.Server.Services;
using Moongate.UO.Commands;
using Moongate.UO.Data.Factory.Json;
using Moongate.UO.Data.Files;
using Moongate.UO.Data.Interfaces.Actions;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Json.Converters;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Packets.Chat;
using Moongate.UO.Data.Packets.Items;
using Moongate.UO.Data.Packets.Login;
using Moongate.UO.Data.Packets.MegaCliloc;
using Moongate.UO.Data.Packets.Mouse;
using Moongate.UO.Data.Packets.System;
using Moongate.UO.Data.Persistence;
using Moongate.UO.Data.Types;
using Moongate.UO.FileLoaders;
using Moongate.UO.Interfaces.Services;
using Moongate.UO.Interfaces.Services.Systems;
using Moongate.UO.Modules;
using Moongate.UO.PacketHandlers;
using Serilog;

JsonUtils.RegisterJsonContext(MoongateCoreServerContext.Default);
JsonUtils.RegisterJsonContext(UOJsonContext.Default);
JsonUtils.RegisterJsonContext(TextJsonTemplateContext.Default);

JsonUtils.AddJsonConverter(new JsonStringEnumConverter<LootType>());
JsonUtils.AddJsonConverter(new JsonStringEnumConverter<Stat>());
JsonUtils.AddJsonConverter(new Point2DConverter());
JsonUtils.AddJsonConverter(new Point3DConverter());
JsonUtils.AddJsonConverter(new SerialConverter());
JsonUtils.AddJsonConverter(new RaceConverter());
JsonUtils.AddJsonConverter(new ProfessionInfoConverter());
JsonUtils.AddJsonConverter(new MapConverter());
JsonUtils.AddJsonConverter(new ClientVersionConverter());
JsonUtils.AddJsonConverter(new FlagsConverter<CharacterListFlags>());
JsonUtils.AddJsonConverter(new FlagsConverter<FeatureFlags>());
JsonUtils.AddJsonConverter(new FlagsConverter<HousingFlags>());
JsonUtils.AddJsonConverter(new FlagsConverter<UOMapSelectionFlags>());

TypeScriptDocumentationGenerator.AddInterfaceToGenerate(typeof(IItemAction));

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
                                               .AddService(typeof(IEntityFactoryService), typeof(EntityFactoryService))
                                               .AddService(typeof(IPersistenceService), typeof(PersistenceService), 100)
                                               .AddService(typeof(IAccountService), typeof(AccountService))
                                               .AddService(typeof(IMegaClilocService), typeof(MegaClilocService))
                                               .AddService(typeof(IMobileService), typeof(MobileService))
                                               .AddService(typeof(IItemService), typeof(ItemService))
                                               .AddService(typeof(IFileLoaderService), typeof(FileLoaderService), -1)
                                               .AddService(typeof(ISpatialWorldService), typeof(SpatialWorldService))
                                               .AddService(
                                                   typeof(IGamePacketHandlerService),
                                                   typeof(GamePacketHandlerService)
                                               )
                                               .AddService(typeof(INotificationSystem), typeof(NotificationSystem))
                                               .AddService(typeof(INameService), typeof(NameService))
                                               .AddService(typeof(ICallbackService), typeof(CallbackService))

                                               //
                                               .AddService(typeof(IEntityFileService), typeof(MoongateEntityFileService))
                                               .AddService(typeof(IAiService), typeof(AiService), 99)
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
                                               scriptEngine.AddScriptModule(typeof(SystemModule));

                                               scriptEngine.AddScriptModule(typeof(CommandsModule));
                                               scriptEngine.AddScriptModule(typeof(LoadScriptModule));
                                               scriptEngine.AddScriptModule(typeof(ConsoleModule));

                                               scriptEngine.AddScriptModule(typeof(CommonEventModule));

                                               scriptEngine.AddScriptModule(typeof(ItemsModule));

                                               scriptEngine.AddScriptModule(typeof(AiModule));
                                           };

        bootstrap.ConfigureNetworkServices += networkService =>
                                              {
                                                  PacketRegistration.RegisterPackets(networkService);
                                                  var gamePacketHandlerService =
                                                      MoongateContext.Container.Resolve<IGamePacketHandlerService>();

                                                  // Registering all packet handlers

                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<LoginRequestPacket, LoginHandler>();
                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<LoginSeedPacket, LoginHandler>();
                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<SelectServerPacket, LoginHandler>();
                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<GameServerLoginPacket, LoginHandler>();
                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<ClientVersionPacket, LoginHandler>();

                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<CharacterCreationPacket,
                                                          CharactersHandler>();
                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<CharacterDeletePacket, CharactersHandler>();
                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<CharacterLoginPacket, CharactersHandler>();

                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<MoveRequestPacket, CharacterMoveHandler>();
                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<MoveAckPacket, CharacterMoveHandler>();

                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<UnicodeSpeechRequestPacket, ChatHandler>();

                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<PingPacket, PingHandler>();

                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<SingleClickPacket, ClickHandler>();
                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<DoubleClickPacket, ClickHandler>();
                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<TargetCursorPacket, ClickHandler>();

                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<MegaClilocRequestPacket, ToolTipHandler>();

                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<DropItemPacket, ItemsHandler>();
                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<PickUpItemPacket, ItemsHandler>();
                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<DropWearItemPacket, ItemsHandler>();
                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<TargetCursorPacket, TargetCursorHandler>();

                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<GetPlayerStatusPacket,
                                                          PlayerStatusHandler>();

                                                  gamePacketHandlerService
                                                      .RegisterGamePacketHandler<TalkRequestPacket, TalkRequestHandler>();
                                              };

        bootstrap.AfterInitialize += (container, config) =>
                                     {
                                         var fileLoaderService = container.Resolve<IFileLoaderService>();
                                         var directoriesConfig = container.Resolve<DirectoriesConfig>();
                                         var commandService = container.Resolve<ICommandSystemService>();
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
                                         fileLoaderService.AddFileLoader<CliLocLoader>();
                                         fileLoaderService.AddFileLoader<ContainersDataLoader>();
                                         fileLoaderService.AddFileLoader<RegionDataLoader>();
                                         fileLoaderService.AddFileLoader<WeatherDataLoader>();
                                         fileLoaderService.AddFileLoader<NamesLoader>();

                                         DefaultCommands.RegisterDefaultCommands(commandService);
                                     };

        bootstrap.Initialize();

        await bootstrap.StartAsync();
    }
);

static async Task CopyAssetsFilesAsync(DirectoriesConfig directoriesConfig)
{
    var assets = ResourceUtils.GetEmbeddedResourceNames(typeof(Program).Assembly, "Assets");
    var files = assets.Select(
                          s => new
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
