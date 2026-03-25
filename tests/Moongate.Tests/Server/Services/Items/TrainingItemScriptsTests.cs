using System.Net.Sockets;
using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Modules;
using Moongate.Server.Services.Interaction;
using Moongate.Server.Services.Items;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using Stat = Moongate.UO.Data.Types.Stat;

namespace Moongate.Tests.Server.Services.Items;

public sealed class TrainingItemScriptsTests
{
    [SetUp]
    public void SetUp()
        => SkillInfo.Table =
        [
            new(
                (int)UOSkillName.Archery,
                "Archery",
                0,
                100,
                0,
                "Archer",
                0,
                0,
                0,
                1,
                "Archery",
                Stat.Dexterity,
                Stat.Strength
            ),
            new(
                (int)UOSkillName.Stealing,
                "Stealing",
                0,
                100,
                0,
                "Thief",
                0,
                0,
                0,
                1,
                "Stealing",
                Stat.Dexterity,
                Stat.Intelligence
            ),
            new(
                (int)UOSkillName.Swords,
                "Swords",
                100,
                0,
                0,
                "Swordsman",
                0,
                0,
                0,
                1,
                "Swords",
                Stat.Strength,
                Stat.Dexterity
            )
        ];

    private sealed class TrainingItemScriptsTestCharacterService : ICharacterService
    {
        public UOMobileEntity? CharacterToReturn { get; set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => Task.CompletedTask;

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult(character.Id);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult(CharacterToReturn?.Id == characterId ? CharacterToReturn : null);

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);
    }

    private sealed class TrainingItemScriptsTestItemService : IItemService
    {
        private readonly Dictionary<Serial, UOItemEntity> _items = [];
        private readonly Dictionary<string, Func<UOItemEntity>> _templateFactories = new(StringComparer.OrdinalIgnoreCase);
        private uint _nextSerial = 0x9000;

        public IReadOnlyCollection<UOItemEntity> Items => _items.Values;

        public void Register(UOItemEntity item)
            => _items[item.Id] = item;

        public void RegisterTemplate(string templateId, Func<UOItemEntity> factory)
            => _templateFactories[templateId] = factory;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
        {
            foreach (var item in items)
            {
                Register(item);
            }

            return Task.CompletedTask;
        }

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
        {
            Register(item);

            return Task.FromResult(item.Id);
        }

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            if (!_items.Remove(itemId, out var item))
            {
                return Task.FromResult(false);
            }

            if (item.ParentContainerId != Serial.Zero &&
                _items.TryGetValue(item.ParentContainerId, out var parent))
            {
                parent.RemoveItem(itemId);
            }

            return Task.FromResult(true);
        }

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => Task.FromResult(true);

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(_items.GetValueOrDefault(itemId));

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(
                _items.TryGetValue(containerId, out var container)
                    ? new List<UOItemEntity>(container.Items)
                    : new List<UOItemEntity>()
            );

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
        {
            if (!_items.TryGetValue(itemId, out var item) || !_items.TryGetValue(containerId, out var container))
            {
                return Task.FromResult(false);
            }

            if (item.ParentContainerId != Serial.Zero &&
                _items.TryGetValue(item.ParentContainerId, out var previousParent))
            {
                previousParent.RemoveItem(itemId);
            }

            container.AddItem(item, position);
            Register(container);
            Register(item);

            return Task.FromResult(true);
        }

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
        {
            if (!_items.TryGetValue(itemId, out var item))
            {
                return Task.FromResult(false);
            }

            item.Location = location;
            item.MapId = mapId;
            item.ParentContainerId = Serial.Zero;
            item.ContainerPosition = Point2D.Zero;
            Register(item);

            return Task.FromResult(true);
        }

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
        {
            if (!_templateFactories.TryGetValue(itemTemplateId, out var factory))
            {
                throw new InvalidOperationException($"No item template factory registered for '{itemTemplateId}'.");
            }

            var item = factory();

            if (item.Id == Serial.Zero)
            {
                item.Id = (Serial)_nextSerial++;
            }

            Register(item);

            return Task.FromResult(item);
        }

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((_items.TryGetValue(itemId, out var item), item));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            Register(item);

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
        {
            foreach (var item in items)
            {
                Register(item);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TrainingItemScriptsTestSpeechService : ISpeechService
    {
        public List<(long SessionId, string Text)> SentMessages { get; } = [];

        public Task<int> BroadcastFromServerAsync(string text, short hue = 946, short font = 3, string language = "ENU")
            => Task.FromResult(0);

        public Task HandleOpenChatWindowAsync(
            GameSession session,
            OpenChatWindowPacket packet,
            CancellationToken cancellationToken = default
        )
            => Task.CompletedTask;

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
            SentMessages.Add((session.SessionId, text));

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
            => Task.FromResult(0);
    }

    private sealed class TrainingItemScriptsTestTimerService : ITimerService
    {
        private readonly Dictionary<string, (Action Callback, bool Repeat)> _timers = [];
        private int _nextId = 1;

        public string RegisterTimer(
            string name,
            TimeSpan interval,
            Action callback,
            TimeSpan? delay = null,
            bool repeat = false
        )
        {
            var timerId = $"{name}:{_nextId++}";
            _timers[timerId] = (callback, repeat);

            return timerId;
        }

        public bool Fire(string timerId)
        {
            if (!_timers.TryGetValue(timerId, out var timer))
            {
                return false;
            }

            timer.Callback();

            if (!timer.Repeat)
            {
                _timers.Remove(timerId);
            }

            return true;
        }

        public void FireAll()
        {
            var timerIds = _timers.Keys.ToArray();

            foreach (var timerId in timerIds)
            {
                Fire(timerId);
            }
        }

        public void ProcessTick() { }

        public void UnregisterAllTimers()
            => _timers.Clear();

        public bool UnregisterTimer(string timerId)
            => _timers.Remove(timerId);

        public int UnregisterTimersByName(string name)
        {
            var matchingTimers = _timers.Keys.Where(key => key.StartsWith($"{name}:", StringComparison.Ordinal)).ToArray();

            foreach (var timerId in matchingTimers)
            {
                _timers.Remove(timerId);
            }

            return matchingTimers.Length;
        }

        public int UpdateTicksDelta(long timestampMilliseconds)
            => 0;
    }

    private sealed class TrainingItemScriptsEnvironment : IDisposable
    {
        public TrainingItemScriptsEnvironment(
            TempDirectory tempDirectory,
            LuaScriptEngineService scriptEngine,
            ItemScriptDispatcher dispatcher,
            GameSession session,
            UOMobileEntity mobile,
            FakeGameNetworkSessionService sessionService,
            TrainingItemScriptsTestCharacterService characterService,
            TrainingItemScriptsTestItemService itemService,
            TrainingItemScriptsTestSpeechService speechService,
            TrainingItemScriptsTestTimerService timerService
        )
        {
            TempDirectory = tempDirectory;
            ScriptEngine = scriptEngine;
            Dispatcher = dispatcher;
            Session = session;
            Mobile = mobile;
            SessionService = sessionService;
            CharacterService = characterService;
            ItemService = itemService;
            SpeechService = speechService;
            TimerService = timerService;
        }

        public TempDirectory TempDirectory { get; }

        public LuaScriptEngineService ScriptEngine { get; }

        public ItemScriptDispatcher Dispatcher { get; }

        public GameSession Session { get; }

        public UOMobileEntity Mobile { get; }

        public FakeGameNetworkSessionService SessionService { get; }

        public TrainingItemScriptsTestCharacterService CharacterService { get; }

        public TrainingItemScriptsTestItemService ItemService { get; }

        public TrainingItemScriptsTestSpeechService SpeechService { get; }

        public TrainingItemScriptsTestTimerService TimerService { get; }

        public void Dispose()
        {
            ScriptEngine.Dispose();
            TempDirectory.Dispose();
        }
    }

    [Test]
    public async Task DispatchAsync_WhenTrainingDummyUsedWithMeleeWeapon_ShouldStartSwingGainSkillAndResetOnTimer()
    {
        using var environment = await CreateEnvironmentAsync(() => 0.0);
        environment.Mobile.InitializeSkills();
        environment.Mobile.SetSkill(UOSkillName.Swords, 0);

        var sword = new UOItemEntity
        {
            Id = (Serial)0x7000,
            Name = "Longsword",
            ItemId = 0x0F61,
            WeaponSkill = UOSkillName.Swords,
            MapId = 1,
            Location = environment.Mobile.Location,
            EquippedLayer = ItemLayerType.OneHanded,
            CombatStats = new()
            {
                DamageMin = 1,
                DamageMax = 4,
                RangeMin = 1,
                RangeMax = 1
            }
        };
        environment.Mobile.AddEquippedItem(ItemLayerType.OneHanded, sword);

        var dummy = new UOItemEntity
        {
            Id = (Serial)0x7100,
            Name = "Training Dummy",
            ScriptId = "items.training_dummy",
            ItemId = 0x1074,
            MapId = 1,
            Location = new(101, 100, 0)
        };
        environment.ItemService.Register(dummy);

        var dispatched = await environment.Dispatcher.DispatchAsync(new(environment.Session, dummy, "double_click"));
        environment.TimerService.FireAll();

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(environment.Mobile.GetSkill(UOSkillName.Swords)!.Base, Is.EqualTo(1));
                Assert.That(dummy.ItemId, Is.EqualTo(0x1074));
            }
        );
    }

    [Test]
    public async Task DispatchAsync_WhenArcheryButteUsedWithBow_ShouldConsumeAmmoStoreShotAndGainSkill()
    {
        using var environment = await CreateEnvironmentAsync(() => 0.0);
        environment.Mobile.InitializeSkills();
        environment.Mobile.SetSkill(UOSkillName.Archery, 0);

        var backpack = new UOItemEntity
        {
            Id = (Serial)0x7200,
            Name = "Backpack",
            ItemId = 0x0E75,
            MapId = 1,
            Location = environment.Mobile.Location
        };
        var arrows = new UOItemEntity
        {
            Id = (Serial)0x7201,
            Name = "Arrow",
            ItemId = 0x0F3F,
            Amount = 3,
            IsStackable = true,
            MapId = 1,
            Location = environment.Mobile.Location
        };
        backpack.AddItem(arrows, new(1, 1));
        environment.Mobile.AddEquippedItem(ItemLayerType.Backpack, backpack);
        environment.Mobile.BackpackId = backpack.Id;

        var bow = new UOItemEntity
        {
            Id = (Serial)0x7202,
            Name = "Bow",
            ItemId = 0x13B2,
            WeaponSkill = UOSkillName.Archery,
            AmmoItemId = 0x0F3F,
            MapId = 1,
            Location = environment.Mobile.Location,
            EquippedLayer = ItemLayerType.TwoHanded,
            CombatStats = new()
            {
                DamageMin = 9,
                DamageMax = 12,
                RangeMin = 1,
                RangeMax = 10
            }
        };
        environment.Mobile.AddEquippedItem(ItemLayerType.TwoHanded, bow);
        environment.ItemService.Register(backpack);
        environment.ItemService.Register(arrows);

        var butte = new UOItemEntity
        {
            Id = (Serial)0x7203,
            Name = "Archery Butte",
            ScriptId = "items.archery_butte",
            ItemId = 0x100A,
            MapId = 1,
            Location = new(100, 100, 0)
        };
        environment.ItemService.Register(butte);
        environment.Mobile.Location = new(105, 100, 0);

        var dispatched = await environment.Dispatcher.DispatchAsync(new(environment.Session, butte, "double_click"));

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(environment.Mobile.GetSkill(UOSkillName.Archery)!.Base, Is.EqualTo(1));
                Assert.That(arrows.Amount, Is.EqualTo(2));
                Assert.That(butte.TryGetCustomDouble("stored_arrows", out var storedArrows), Is.True);
                Assert.That(storedArrows, Is.EqualTo(1).Within(0.001));
                Assert.That(butte.TryGetCustomDouble($"shots_{(uint)environment.Mobile.Id}", out var shots), Is.True);
                Assert.That(shots, Is.EqualTo(1).Within(0.001));
            }
        );
    }

    [Test]
    public async Task DispatchAsync_WhenArcheryButteHasStoredAmmoAndPlayerIsNearby_ShouldReturnAmmoToBackpack()
    {
        using var environment = await CreateEnvironmentAsync(() => 0.0);

        var backpack = new UOItemEntity
        {
            Id = (Serial)0x7300,
            Name = "Backpack",
            ItemId = 0x0E75,
            MapId = 1,
            Location = environment.Mobile.Location
        };
        environment.Mobile.AddEquippedItem(ItemLayerType.Backpack, backpack);
        environment.Mobile.BackpackId = backpack.Id;
        environment.ItemService.Register(backpack);

        environment.ItemService.RegisterTemplate(
            "arrow",
            () => new()
            {
                Name = "Arrow",
                ItemId = 0x0F3F,
                Amount = 1,
                IsStackable = true,
                MapId = 1,
                Location = Point3D.Zero
            }
        );
        environment.ItemService.RegisterTemplate(
            "bolt",
            () => new()
            {
                Name = "Bolt",
                ItemId = 0x1BFB,
                Amount = 1,
                IsStackable = true,
                MapId = 1,
                Location = Point3D.Zero
            }
        );

        var butte = new UOItemEntity
        {
            Id = (Serial)0x7301,
            Name = "Archery Butte",
            ScriptId = "items.archery_butte",
            ItemId = 0x100A,
            MapId = 1,
            Location = new(100, 100, 0)
        };
        butte.SetCustomInteger("stored_arrows", 2);
        butte.SetCustomInteger("stored_bolts", 1);
        environment.ItemService.Register(butte);
        environment.Mobile.Location = new(100, 101, 0);

        var dispatched = await environment.Dispatcher.DispatchAsync(new(environment.Session, butte, "double_click"));
        var backpackItems = backpack.Items.ToArray();
        var gatheredArrows = backpackItems.Single(item => item.ItemId == 0x0F3F);
        var gatheredBolts = backpackItems.Single(item => item.ItemId == 0x1BFB);

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(gatheredArrows.Amount, Is.EqualTo(2));
                Assert.That(gatheredBolts.Amount, Is.EqualTo(1));
                Assert.That(butte.TryGetCustomDouble("stored_arrows", out var storedArrows), Is.True);
                Assert.That(storedArrows, Is.EqualTo(0).Within(0.001));
                Assert.That(butte.TryGetCustomDouble("stored_bolts", out var storedBolts), Is.True);
                Assert.That(storedBolts, Is.EqualTo(0).Within(0.001));
            }
        );
    }

    [Test]
    public async Task DispatchAsync_WhenPickpocketDipCheckFails_ShouldStartSwingAndResetOnTimer()
    {
        using var environment = await CreateEnvironmentAsync(() => 0.99);
        environment.Mobile.InitializeSkills();
        environment.Mobile.SetSkill(UOSkillName.Stealing, 0);

        var dip = new UOItemEntity
        {
            Id = (Serial)0x7400,
            Name = "Pickpocket Dip",
            ScriptId = "items.pickpocket_dip",
            ItemId = 0x1EC0,
            MapId = 1,
            Location = new(101, 100, 0)
        };
        environment.ItemService.Register(dip);

        var dispatched = await environment.Dispatcher.DispatchAsync(new(environment.Session, dip, "double_click"));

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(dip.ItemId, Is.EqualTo(0x1EC1));
                Assert.That(environment.SpeechService.SentMessages.Select(message => message.Text), Has.Some.Contains("swinging"));
            }
        );

        environment.TimerService.FireAll();

        Assert.That(dip.ItemId, Is.EqualTo(0x1EC0));
    }

    private static string GetRepositoryItemScriptPath(string fileName)
        => Path.GetFullPath(
            Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "moongate_data",
                "scripts",
                "items",
                fileName
            )
        );

    private static void CopyRequiredItemScript(string scriptsDirectory, string fileName)
        => File.Copy(
            GetRepositoryItemScriptPath(fileName),
            Path.Combine(scriptsDirectory, "items", fileName),
            true
        );

    private static async Task<TrainingItemScriptsEnvironment> CreateEnvironmentAsync(Func<double> skillCheckRollProvider)
    {
        var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDirectory = directories[DirectoryType.Scripts];
        Directory.CreateDirectory(scriptsDirectory);
        Directory.CreateDirectory(Path.Combine(scriptsDirectory, "items"));

        await File.WriteAllTextAsync(
            Path.Combine(scriptsDirectory, "init.lua"),
            """
            require("items.training_dummy")
            require("items.archery_butte")
            require("items.pickpocket_dip")
            """
        );

        CopyRequiredItemScript(scriptsDirectory, "training_dummy.lua");
        CopyRequiredItemScript(scriptsDirectory, "archery_butte.lua");
        CopyRequiredItemScript(scriptsDirectory, "pickpocket_dip.lua");

        var sessionService = new FakeGameNetworkSessionService();
        var speechService = new TrainingItemScriptsTestSpeechService();
        var characterService = new TrainingItemScriptsTestCharacterService();
        var itemService = new TrainingItemScriptsTestItemService();
        var timerService = new TrainingItemScriptsTestTimerService();
        ISkillGainService skillGainService = new SkillGainService(() => 0.0);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x6000,
            Name = "Trainer",
            IsPlayer = true,
            IsAlive = true,
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var session = new GameSession(new(client))
        {
            CharacterId = mobile.Id,
            Character = mobile
        };
        sessionService.Add(session);
        characterService.CharacterToReturn = mobile;

        var container = new Container();
        container.RegisterInstance<IGameNetworkSessionService>(sessionService);
        container.RegisterInstance<ISpeechService>(speechService);
        container.RegisterInstance<ICharacterService>(characterService);
        container.RegisterInstance<IItemService>(itemService);
        container.RegisterInstance<ITimerService>(timerService);
        container.RegisterInstance<ISkillGainService>(skillGainService);
        container.RegisterInstance<Func<double>>(skillCheckRollProvider);
        container.RegisterInstance<ISpatialWorldService>(new RegionDataLoaderTestSpatialWorldService());
        container.RegisterInstance(
            new MoongateSpatialConfig
            {
                LightWorldStartUtc = "1997-09-01T00:00:00Z",
                LightSecondsPerUoMinute = 5
            }
        );

        var scriptEngine = new LuaScriptEngineService(
            directories,
            [
                new(typeof(ItemModule)),
                new(typeof(MobileModule)),
                new(typeof(SpeechModule)),
                new(typeof(TimeModule)),
                new(typeof(TimerModule))
            ],
            container,
            new(temp.Path, scriptsDirectory, "0.1.0"),
            []
        );
        await scriptEngine.StartAsync();

        var dispatcher = new ItemScriptDispatcher(scriptEngine, itemService, sessionService);

        return new(
            temp,
            scriptEngine,
            dispatcher,
            session,
            mobile,
            sessionService,
            characterService,
            itemService,
            speechService,
            timerService
        );
    }
}
