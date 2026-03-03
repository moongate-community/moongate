using System.Net.Sockets;
using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Modules;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Modules;
using Moongate.Server.Types.Commands;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Scripting;

public class LuaScriptEngineServiceTests
{
    private sealed class LuaScriptEngineServiceTestsCommandSystemService : ICommandSystemService
    {
        public Func<CommandSystemContext, Task>? RegisteredHandler { get; private set; }

        public string? RegisteredCommandName { get; private set; }

        public string? RegisteredDescription { get; private set; }

        public CommandSourceType RegisteredSource { get; private set; }

        public AccountType RegisteredMinimumAccountType { get; private set; }

        public Task ExecuteCommandAsync(
            string commandWithArgs,
            CommandSourceType source = CommandSourceType.Console,
            GameSession? session = null,
            CancellationToken cancellationToken = default
        )
            => Task.CompletedTask;

        public IReadOnlyList<string> GetAutocompleteSuggestions(string commandWithArgs)
            => [];

        public void RegisterCommand(
            string commandName,
            Func<CommandSystemContext, Task> handler,
            string description = "",
            CommandSourceType source = CommandSourceType.Console,
            AccountType minimumAccountType = AccountType.Administrator,
            Func<CommandAutocompleteContext, IReadOnlyList<string>>? autocompleteProvider = null
        )
        {
            RegisteredCommandName = commandName;
            RegisteredHandler = handler;
            RegisteredDescription = description;
            RegisteredSource = source;
            RegisteredMinimumAccountType = minimumAccountType;
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class LuaScriptEngineServiceTestsSpeechService : ISpeechService
    {
        public int BroadcastCalls { get; private set; }

        public int SendCalls { get; private set; }

        public string? LastText { get; private set; }

        public long? LastSessionId { get; private set; }

        public Task<int> BroadcastFromServerAsync(
            string text,
            short hue = 946,
            short font = 3,
            string language = "ENU"
        )
        {
            BroadcastCalls++;
            LastText = text;

            return Task.FromResult(3);
        }

        public Task HandleOpenChatWindowAsync(
            GameSession session,
            OpenChatWindowPacket packet,
            CancellationToken cancellationToken = default
        )
        {
            _ = session;
            _ = packet;
            _ = cancellationToken;

            return Task.CompletedTask;
        }

        public Task<UnicodeSpeechMessagePacket?> ProcessIncomingSpeechAsync(
            GameSession session,
            UnicodeSpeechPacket speechPacket,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<UnicodeSpeechMessagePacket?>(null);

        public Task<bool> SendMessageFromServerAsync(
            GameSession session,
            string text,
            short hue = 946,
            short font = 3,
            string language = "ENU"
        )
        {
            SendCalls++;
            LastSessionId = session.SessionId;
            LastText = text;

            return Task.FromResult(true);
        }

        public Task<int> SpeakAsMobileAsync(
            UOMobileEntity speaker,
            string text,
            int range = 12,
            ChatMessageType messageType = ChatMessageType.Regular,
            short hue = SpeechHues.Default,
            short font = SpeechHues.DefaultFont,
            string language = "ENU",
            CancellationToken cancellationToken = default
        )
        {
            _ = speaker;
            _ = range;
            _ = messageType;
            _ = hue;
            _ = font;
            _ = language;
            _ = cancellationToken;
            SendCalls++;
            LastText = text;

            return Task.FromResult(1);
        }
    }

    private sealed class LuaScriptEngineServiceTestsCharacterService : ICharacterService
    {
        public Serial ExistingCharacterId { get; set; }
        public UOMobileEntity? CharacterToReturn { get; set; }
        public Serial LastRequestedCharacterId { get; private set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
        {
            _ = accountId;
            _ = characterId;

            return Task.FromResult(true);
        }

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
        {
            _ = character;

            return Task.FromResult((Serial)1u);
        }

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
        {
            _ = characterId;
            _ = shirtHue;
            _ = pantsHue;

            return Task.CompletedTask;
        }

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
        {
            _ = character;

            return Task.FromResult<UOItemEntity?>(null);
        }

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
        {
            LastRequestedCharacterId = characterId;

            return Task.FromResult(characterId == ExistingCharacterId ? CharacterToReturn : null);
        }

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
        {
            _ = accountId;

            return Task.FromResult(new List<UOMobileEntity>());
        }

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
        {
            _ = accountId;
            _ = characterId;

            return Task.FromResult(true);
        }
    }

    private sealed class LuaScriptEngineServiceTestsItemService : IItemService
    {
        public Serial ExistingItemId { get; set; }
        public UOItemEntity? ItemToReturn { get; set; }
        public Serial LastRequestedItemId { get; private set; }

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
        {
            _ = generateNewSerial;

            return item;
        }

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
        {
            _ = itemId;
            _ = generateNewSerial;

            return Task.FromResult<UOItemEntity?>(null);
        }

        public Task<Serial> CreateItemAsync(UOItemEntity item)
        {
            _ = item;

            return Task.FromResult((Serial)1u);
        }

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
        {
            _ = itemTemplateId;

            return Task.FromResult(new UOItemEntity { Id = (Serial)1u });
        }

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            _ = itemId;

            return Task.FromResult(true);
        }

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
        {
            _ = itemId;
            _ = location;
            _ = mapId;

            return Task.FromResult<DropItemToGroundResult?>(null);
        }

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
        {
            _ = itemId;
            _ = mobileId;
            _ = layer;

            return Task.FromResult(true);
        }

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
        {
            _ = mapId;
            _ = sectorX;
            _ = sectorY;

            return Task.FromResult(new List<UOItemEntity>());
        }

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
        {
            LastRequestedItemId = itemId;

            return Task.FromResult(itemId == ExistingItemId ? ItemToReturn : null);
        }

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
        {
            LastRequestedItemId = itemId;
            var item = itemId == ExistingItemId ? ItemToReturn : null;

            return Task.FromResult((item is not null, item));
        }

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
        {
            _ = containerId;

            return Task.FromResult(new List<UOItemEntity>());
        }

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
        {
            _ = itemId;
            _ = containerId;
            _ = position;

            return Task.FromResult(true);
        }

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
        {
            _ = itemId;
            _ = location;
            _ = mapId;

            return Task.FromResult(true);
        }

        public Task UpsertItemAsync(UOItemEntity item)
        {
            _ = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
        {
            _ = items;

            return Task.CompletedTask;
        }
    }

    [Test]
    public void AddCallback_AndExecuteCallback_ShouldInvokeCallback()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        object[]? captured = null;

        service.AddCallback("onTest", args => captured = args);
        service.ExecuteCallback("onTest", 1, "two");

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Length, Is.EqualTo(2));
        Assert.That(captured[0], Is.EqualTo(1));
        Assert.That(captured[1], Is.EqualTo("two"));
    }

    [Test]
    public void AddConstant_ShouldExposeNormalizedGlobal()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);

        service.AddConstant("myValue", 42);
        var result = service.ExecuteFunction("MY_VALUE");

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo(42d));
            }
        );
    }

    [Test]
    public void AddManualModuleFunction_ShouldBeCallableFromLua()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);

        service.AddManualModuleFunction<int, int>("math", "double", static value => value * 2);
        var result = service.ExecuteFunction("math.double(21)");

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo(42d));
            }
        );
    }

    [Test]
    public void ExecuteFunction_WhenLuaError_ShouldReturnErrorAndRaiseEvent()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        ScriptErrorInfo? capturedError = null;
        service.OnScriptError += (_, info) => capturedError = info;

        var result = service.ExecuteFunction("unknown_function()");

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Is.Not.Empty);
                Assert.That(capturedError, Is.Not.Null);
            }
        );
    }

    [Test]
    public void ExecuteScript_WhenScriptIsValid_ShouldReuseCompiledChunkFromCache()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        const string script = "local x = 1 + 1";

        service.ExecuteScript(script);
        service.ExecuteScript(script);

        var metrics = service.GetExecutionMetrics();

        Assert.Multiple(
            () =>
            {
                Assert.That(metrics.CacheHits, Is.EqualTo(1));
                Assert.That(metrics.CacheMisses, Is.EqualTo(1));
                Assert.That(metrics.TotalScriptsCached, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void ExecuteScript_WhenSyntaxIsInvalid_ShouldNotCacheFailedCompilation()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        const string invalidScript = "local x =";

        Assert.That(() => service.ExecuteScript(invalidScript), Throws.Exception);
        Assert.That(() => service.ExecuteScript(invalidScript), Throws.Exception);

        var metrics = service.GetExecutionMetrics();

        Assert.Multiple(
            () =>
            {
                Assert.That(metrics.CacheHits, Is.EqualTo(0));
                Assert.That(metrics.CacheMisses, Is.EqualTo(2));
                Assert.That(metrics.TotalScriptsCached, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public void ExecuteScriptFile_WhenFileMissing_ShouldThrowFileNotFoundException()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        var file = Path.Combine(temp.Path, "scripts", "missing.lua");

        Assert.That(() => service.ExecuteScriptFile(file), Throws.TypeOf<FileNotFoundException>());
    }

    [Test]
    public async Task StartAsync_ShouldGenerateDefinitionsAndLuarcUnderConfiguredLuarcDirectory()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(luarcDir);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(LogModule))],
            new Container(),
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        var definitionsPath = Path.Combine(luarcDir, "definitions.lua");
        var luarcPath = Path.Combine(luarcDir, ".luarc.json");

        Assert.Multiple(
            () =>
            {
                Assert.That(File.Exists(definitionsPath), Is.True);
                Assert.That(File.Exists(luarcPath), Is.True);
            }
        );

        var luarcContent = await File.ReadAllTextAsync(luarcPath);
        Assert.That(luarcContent, Does.Contain(scriptsDir));
        Assert.That(luarcContent, Does.Contain(luarcDir));
    }

    [Test]
    public async Task StartAsync_WithCommandModule_ShouldRegisterLuaCommand()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(luarcDir);

        var commandSystemService = new LuaScriptEngineServiceTestsCommandSystemService();
        var container = new Container();
        container.RegisterInstance<ICommandSystemService>(commandSystemService);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(CommandModule))],
            container,
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();
        var registrationResult = service.ExecuteFunction(
            """
            (function()
                command.register("lua_test", function(ctx)
                    captured_command_text = ctx.command_text
                end, {
                    description = "lua command",
                    source = 3,
                    minimum_account_type = 2
                })
                return true
            end)()
            """
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(registrationResult.Success, Is.True);
                Assert.That(commandSystemService.RegisteredCommandName, Is.EqualTo("lua_test"));
                Assert.That(commandSystemService.RegisteredDescription, Is.EqualTo("lua command"));
                Assert.That(
                    commandSystemService.RegisteredSource,
                    Is.EqualTo(CommandSourceType.Console | CommandSourceType.InGame)
                );
                Assert.That(commandSystemService.RegisteredMinimumAccountType, Is.EqualTo(AccountType.Administrator));
                Assert.That(commandSystemService.RegisteredHandler, Is.Not.Null);
            }
        );

        var commandContext = new CommandSystemContext(
            "lua_test foo",
            ["foo"],
            CommandSourceType.InGame,
            11,
            (_, _) => { }
        );
        await commandSystemService.RegisteredHandler!(commandContext);

        var callbackResult = service.ExecuteFunction("captured_command_text");
        Assert.Multiple(
            () =>
            {
                Assert.That(callbackResult.Success, Is.True);
                Assert.That(callbackResult.Data, Is.EqualTo("lua_test foo"));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithGumpModule_ShouldBuildLayoutFromLua()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(luarcDir);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(GumpModule))],
            new Container(),
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        var result = service.ExecuteFunction(
            """
            (function()
                local g = gump.create()
                g:ResizePic(0, 0, 9200, 300, 200)
                g:Text(80, 15, 0x480, "The Blacksmith")
                g:Text(30, 50, 0, "What dost thou require?")
                return g:BuildLayout()
            end)()
            """
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(
                    result.Data,
                    Is.EqualTo("{ resizepic 0 0 9200 300 200 } { text 80 15 1152 0 } { text 30 50 0 1 }")
                );
            }
        );
    }

    [Test]
    public async Task StartAsync_WithGumpModule_ShouldExposeTextEntriesFromLua()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(luarcDir);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(GumpModule))],
            new Container(),
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        var result = service.ExecuteFunction(
            """
            (function()
                local g = gump.create()
                g:Text(10, 10, 0, "First")
                g:Text(10, 25, 0, "Second")
                local texts = g:BuildTexts()
                return texts[1] .. "|" .. texts[2]
            end)()
            """
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo("First|Second"));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithLogModule_ShouldKeepLogAsTable()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(luarcDir);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(LogModule))],
            new Container(),
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        var typeResult = service.ExecuteFunction("type(log)");
        var callResult = service.ExecuteFunction("log.info('hello')");

        Assert.Multiple(
            () =>
            {
                Assert.That(typeResult.Success, Is.True);
                Assert.That(typeResult.Data, Is.EqualTo("table"));
                Assert.That(callResult.Success, Is.True);
            }
        );
    }

    [Test]
    public async Task StartAsync_WithMobileAndItemModules_ShouldResolveReferencesOrNil()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(luarcDir);

        var sessionService = new FakeGameNetworkSessionService();
        var speechService = new LuaScriptEngineServiceTestsSpeechService();
        var characterService = new LuaScriptEngineServiceTestsCharacterService
        {
            ExistingCharacterId = (Serial)0x555,
            CharacterToReturn = new()
            {
                Id = (Serial)0x555,
                Name = "Script Mobile"
            }
        };
        var itemService = new LuaScriptEngineServiceTestsItemService
        {
            ExistingItemId = (Serial)0x777,
            ItemToReturn = new()
            {
                Id = (Serial)0x777,
                Name = "Script Item",
                Amount = 10,
                ItemId = 0x0EED
            }
        };
        var container = new Container();
        container.RegisterInstance<IGameNetworkSessionService>(sessionService);
        container.RegisterInstance<ISpeechService>(speechService);
        container.RegisterInstance<ICharacterService>(characterService);
        container.RegisterInstance<IItemService>(itemService);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(MobileModule)), new(typeof(ItemModule))],
            container,
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();
        var result = service.ExecuteFunction(
            """
            (function()
                local m = mobile.get(0x555)
                local i = item.get(0x777)
                local missingMobile = mobile.get(0x999)
                local missingItem = item.get(0x998)
                return m ~= nil and i ~= nil and missingMobile == nil and missingItem == nil
            end)()
            """
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo(true));
                Assert.That(characterService.LastRequestedCharacterId, Is.EqualTo((Serial)0x999));
                Assert.That(itemService.LastRequestedItemId, Is.EqualTo((Serial)0x998));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithSpeechModule_ShouldInvokeSendSayBroadcast()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(luarcDir);

        var sessionService = new FakeGameNetworkSessionService();
        var speechService = new LuaScriptEngineServiceTestsSpeechService();
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x100
        };
        sessionService.Add(session);

        var container = new Container();
        container.RegisterInstance<IGameNetworkSessionService>(sessionService);
        container.RegisterInstance<ISpeechService>(speechService);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(SpeechModule))],
            container,
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();
        var result = service.ExecuteFunction(
            $"""
             (function()
                 local ok1 = speech.send({session.SessionId}, "hello")
                 local ok2 = speech.say({(uint)session.CharacterId}, "say hello")
                 local recipients = speech.broadcast("broadcast hello")
                 return ok1 and ok2 and recipients == 3
             end)()
             """
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo(true));
                Assert.That(speechService.SendCalls, Is.EqualTo(2));
                Assert.That(speechService.BroadcastCalls, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void ToScriptEngineFunctionName_ShouldConvertToSnakeCase()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);

        var name = service.ToScriptEngineFunctionName("HelloWorldMethod");

        Assert.That(name, Is.EqualTo("hello_world_method"));
    }

    private static LuaScriptEngineService CreateService(string rootPath)
    {
        var dirs = new DirectoriesConfig(rootPath, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = rootPath;
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(luarcDir);

        return new(
            dirs,
            [],
            new Container(),
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );
    }
}
