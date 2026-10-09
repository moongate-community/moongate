using DryIoc;
using Lua;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Guilds;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Vendors;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Vendors;

/// <summary>
///     The shipped <c>shopkeeper.lua</c> with the real npc, mobile and vendor modules: the context menu entry, the
///     words "vendor buy", and the window that opens.
/// </summary>
public sealed class ShopkeeperScriptIntegrationTests : IAsyncLifetime
{
    private static readonly Serial VendorId = new(0x100);

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly RecordingVendorService _vendors = new();
    private readonly RecordingTrainingService _training = new();
    private readonly RecordingNpcGuildService _guilds = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingTimerService _timers = new();
    private readonly SettableClock _clock = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _baker = null!;
    private LuaScriptEngineService _engine = null!;
    private NpcScriptService _npcScripts = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var aria));
        _aria = aria;
        _aria.AccountId = new Serial(1002);
        _aria.Location = new Point3D(1602, 1600, 0);
        _baker = new MobileEntity
        {
            Id = VendorId, Name = "a baker", TemplateId = "baker", Map = MapType.Trammel,
            Location = new Point3D(1600, 1600, 0)
        };
        _fixture.Mobiles.EnterWorld(_baker);

        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_fixture.Network.Loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<IWorldViewService>(new RecordingWorldViewService());
        _container.RegisterInstance<IVendorService>(_vendors);
        _container.RegisterInstance<ITrainingService>(_training);
        _container.RegisterInstance<INpcGuildService>(_guilds);
        _container.RegisterInstance<IItemService>(TestItems.Create(_fixture.Sectors));
        _container.RegisterInstance<TimeProvider>(_clock);
        _container.RegisterInstance<ITeleportService>(new RecordingTeleportService());
        _container.RegisterInstance<ICrimeService>(new RecordingCrimeService());
        var templates = new MobileTemplateService(
            new StubDataLoaderService().With(new MobileTemplate { Id = "baker", ScriptId = "shopkeeper" })
        );
        _container.RegisterInstance<IMobileTemplateService>(templates);
        _container.AddScriptModule<NpcModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<VendorModule>();
        _container.AddScriptModule<TrainerModule>();
        _container.AddScriptModule<NpcGuildModule>();
        _container.RegisterScriptEnum<SkillType>();
        _container.RegisterScriptEnum<SpeechKeywordType>();
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );
        _scripts.Write("mobiles/shopkeeper.lua", File.ReadAllText(ShippedScript("mobiles/shopkeeper.lua")));
        _scripts.Write("common/training.lua", File.ReadAllText(ShippedScript("common/training.lua")));
        _scripts.Write("common/guild.lua", File.ReadAllText(ShippedScript("common/guild.lua")));
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path,
            MaxInstructionsPerResume = 20_000,
            MaxInstructionsPerChunk = 100_000,
            HookInterval = 100,
            WriteDefinitions = false
        };
        _engine = new(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _fixture.Network.Loop,
            _timers,
            new EventBusAdapter(_container)
        );
        await _engine.StartAsync();
        _npcScripts = new NpcScriptService(_engine, templates, _fixture.Network.Loop, options);
        await _npcScripts.StartAsync();
    }

    [Fact]
    public async Task TheContextMenu_OffersBuyAndSell_FromEightTiles()
    {
        var result = await RunAsync("on_context_menu", (long)_aria.Id.Value);

        Assert.Empty(_errors);
        var entries = Assert.IsType<LuaTable>(Assert.Single(result.Values));
        Assert.Equal(2, entries.ArrayLength);
        Assert.Equal(
            [("buy", 3006103, 8), ("sell", 3006104, 8)],
            Enumerable.Range(1, 2)
                .Select(index => entries[index].Read<LuaTable>())
                .Select(entry => (entry["id"].Read<string>(), entry["cliloc"].Read<int>(), entry["range"].Read<int>()))
        );
    }

    [Theory]
    [InlineData(true, false, "buy")]
    [InlineData(false, true, "sell")]
    [InlineData(false, false, "")]
    public async Task TheContextMenu_OffersBuyOnlyWhenTheVendorSells_AndSellOnlyWhenItBuys(
        bool sells,
        bool buys,
        string expected
    )
    {
        _vendors.HasGoods = sells;
        _vendors.WantsGoods = buys;

        var result = await RunAsync("on_context_menu", (long)_aria.Id.Value);

        Assert.Empty(_errors);
        var entries = Assert.IsType<LuaTable>(Assert.Single(result.Values));
        Assert.Equal(
            expected,
            string.Join(',', Enumerable.Range(1, entries.ArrayLength).Select(index => entries[index].Read<LuaTable>()["id"].Read<string>()))
        );
    }

    [Fact]
    public async Task TheContextMenu_OffersNothingToAGhost()
    {
        _aria.Body = 0x192;

        var result = await RunAsync("on_context_menu", (long)_aria.Id.Value);

        Assert.Empty(_errors);
        Assert.Equal(0, Assert.IsType<LuaTable>(Assert.Single(result.Values)).ArrayLength);
    }

    [Fact]
    public async Task PickingBuy_OpensTheWindow_AnythingElseDoesNot()
    {
        await RunAsync("on_context_menu_select", (long)_aria.Id.Value, "sell");
        await RunAsync("on_context_menu_select", (long)_aria.Id.Value, "buy");

        Assert.Empty(_errors);
        Assert.Equal((_session, _baker), Assert.Single(_vendors.Opened));
    }

    [Fact]
    public async Task PickingSell_OffersTheSellList()
    {
        await RunAsync("on_context_menu_select", (long)_aria.Id.Value, "sell");

        Assert.Empty(_errors);
        Assert.Equal((_session, _baker), Assert.Single(_vendors.OpenedSell));
        Assert.Empty(_vendors.Opened);
    }

    [Fact]
    public async Task TheWordsVendorSell_OfferTheSellList_WithinFourTiles()
    {
        await HearAsync("vendor sell", SpeechKeywordType.VendorSell);

        Assert.Empty(_errors);
        Assert.Equal((_session, _baker), Assert.Single(_vendors.OpenedSell));
    }

    [Fact]
    public async Task TheWordsVendorBuy_OpenTheWindow_WithinFourTiles()
    {
        await HearAsync("vendor buy", SpeechKeywordType.VendorBuy);

        Assert.Empty(_errors);
        Assert.Equal((_session, _baker), Assert.Single(_vendors.Opened));
    }

    [Fact]
    public async Task TheWordsVendorBuy_FromFiveTiles_OrWithoutTheKeyword_DoNothing()
    {
        await HearAsync("hello", SpeechKeywordType.Bank);
        _aria.Location = new Point3D(1605, 1600, 0);
        await HearAsync("vendor buy", SpeechKeywordType.VendorBuy);

        Assert.Empty(_errors);
        Assert.Empty(_vendors.Opened);
    }

    [Fact]
    public async Task TwoVendorsHearingTheSameWords_OneOpensTheWindow()
    {
        _fixture.Mobiles.EnterWorld(
            new MobileEntity
            {
                Id = new Serial(0x101), Name = "another baker", TemplateId = "baker", Map = MapType.Trammel,
                Location = new Point3D(1601, 1601, 0)
            }
        );
        await HearAsync("vendor buy", SpeechKeywordType.VendorBuy);

        Assert.Empty(_errors);
        Assert.Single(_vendors.Opened);
    }

    [Fact]
    public async Task TheContextMenu_AddsATrainEntryForEachSkillTheVendorTeaches()
    {
        _training.Skills.AddRange([SkillType.Alchemy, SkillType.Tailoring]);

        var result = await RunAsync("on_context_menu", (long)_aria.Id.Value);

        Assert.Empty(_errors);
        var entries = Assert.IsType<LuaTable>(Assert.Single(result.Values));
        Assert.Equal(4, entries.ArrayLength);
        var train = entries[3].Read<LuaTable>();
        Assert.Equal(
            ("train:0", 3006000, 8),
            (train["id"].Read<string>(), train["cliloc"].Read<int>(), train["range"].Read<int>())
        );
        Assert.Equal(
            ("train:" + (int)SkillType.Tailoring, 3006000 + (int)SkillType.Tailoring),
            (entries[4].Read<LuaTable>()["id"].Read<string>(), entries[4].Read<LuaTable>()["cliloc"].Read<int>())
        );
    }

    [Fact]
    public async Task PickingATrainEntry_AsksTheQuoteOfThatSkill()
    {
        await RunAsync("on_context_menu_select", (long)_aria.Id.Value, "train:" + (int)SkillType.Tailoring);

        Assert.Empty(_errors);
        Assert.Equal((_baker, SkillType.Tailoring), Assert.Single(_training.Quoted));
        Assert.Empty(_vendors.Opened);
    }

    [Fact]
    public async Task TheWordTrain_ListsTheSkills_OrSaysThereIsNothing()
    {
        _training.Skills.Add(SkillType.Alchemy);
        await HearAsync("train", SpeechKeywordType.Train);

        Assert.Empty(_errors);
        Assert.Equal([1043058, 1043059], _speech.SaidClilocs.Select(said => said.Cliloc));

        _speech.SaidClilocs.Clear();
        _training.Skills.Clear();
        _clock.Advance(TimeSpan.FromSeconds(2));
        await HearAsync("train", SpeechKeywordType.Train);

        Assert.Equal(501505, Assert.Single(_speech.SaidClilocs).Cliloc);
    }

    [Fact]
    public async Task GoldDroppedOnTheVendor_IsGivenToTheTrainingService_AndItsAnswerIsTheAnswerOfTheDrop()
    {
        var items = _container.Resolve<IItemService>();
        var gold = new ItemEntity { Id = new Serial(0x40000001), TemplateId = "gold", ItemId = 0x0EED, Amount = 100 };
        items.Add([gold]);

        var taken = await RunAsync("on_drag_drop", (long)_aria.Id.Value, (long)gold.Id.Value);
        _training.Answer = false;
        var refused = await RunAsync("on_drag_drop", (long)_aria.Id.Value, (long)gold.Id.Value);

        Assert.Empty(_errors);
        Assert.Equal(
            [true, false],
            new[] { Assert.IsType<bool>(Assert.Single(taken.Values)), Assert.IsType<bool>(Assert.Single(refused.Values)) }
        );
        Assert.Equal(2, _training.Paid.Count);
    }

    [Fact]
    public async Task ANamedJoin_WithinTwoTiles_AsksTheGuildmasterToQuote_AndAWrongNameDoesNot()
    {
        _guilds.Guild = NpcGuildType.Blacksmiths;
        _aria.Location = new Point3D(1601, 1600, 0);

        await HearAsync("a baker join", SpeechKeywordType.Join);
        await HearAsync("someone join", SpeechKeywordType.Join);
        _aria.Location = new Point3D(1603, 1600, 0);
        _clock.Advance(TimeSpan.FromSeconds(2));
        await HearAsync("a baker join", SpeechKeywordType.Join);

        Assert.Empty(_errors);
        Assert.Equal([_aria], _guilds.Quoted);
    }

    [Fact]
    public async Task ANamedResign_AsksTheGuildmasterToLetTheMemberLeave()
    {
        _guilds.Guild = NpcGuildType.Blacksmiths;
        _aria.Location = new Point3D(1601, 1600, 0);

        await HearAsync("A Baker, I resign", SpeechKeywordType.Resign);

        Assert.Empty(_errors);
        Assert.Equal([_aria], _guilds.Resigned);
    }

    [Fact]
    public async Task GoldDroppedOnAGuildmaster_JoinsTheGuild_BeforeItPaysForALesson()
    {
        _guilds.Guild = NpcGuildType.Blacksmiths;
        var gold = new ItemEntity { Id = new Serial(0x40000002), TemplateId = "gold", ItemId = 0x0EED, Amount = 500 };
        _container.Resolve<IItemService>().Add([gold]);

        var taken = await RunAsync("on_drag_drop", (long)_aria.Id.Value, (long)gold.Id.Value);

        Assert.Empty(_errors);
        Assert.True(Assert.IsType<bool>(Assert.Single(taken.Values)));
        Assert.Equal([gold], _guilds.Joined);
        Assert.Empty(_training.Paid);
    }

    [Fact]
    public async Task GoldOfTheGuildsPriceDroppedOnAGuildmaster_NeverPaysForALesson_EvenWhenTheJoinIsRefused()
    {
        _guilds.Guild = NpcGuildType.Blacksmiths;
        _guilds.Answer = false;
        var gold = new ItemEntity { Id = new Serial(0x40000003), TemplateId = "gold", ItemId = 0x0EED, Amount = 500 };
        _container.Resolve<IItemService>().Add([gold]);

        var taken = await RunAsync("on_drag_drop", (long)_aria.Id.Value, (long)gold.Id.Value);

        Assert.Empty(_errors);
        Assert.False(Assert.IsType<bool>(Assert.Single(taken.Values)));
        Assert.Equal([gold], _guilds.Joined);
        Assert.Empty(_training.Paid);
    }

    [Fact]
    public async Task OtherGoldDroppedOnAGuildmaster_PaysForALesson()
    {
        _guilds.Guild = NpcGuildType.Blacksmiths;
        var gold = new ItemEntity { Id = new Serial(0x40000004), TemplateId = "gold", ItemId = 0x0EED, Amount = 120 };
        _container.Resolve<IItemService>().Add([gold]);

        await RunAsync("on_drag_drop", (long)_aria.Id.Value, (long)gold.Id.Value);

        Assert.Empty(_errors);
        Assert.Empty(_guilds.Joined);
        Assert.Equal([gold], _training.Paid.Select(paid => paid.Gold));
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        await _fixture.DisposeAsync();
    }

    private async Task<ScriptResult> RunAsync(string function, params object?[] args)
    {
        var result = ScriptResult.Missing;
        await _fixture.Network.ExecuteOnLoopAsync(() => result = _npcScripts.Run(_baker, function, args));

        return result;
    }

    private async Task HearAsync(string text, SpeechKeywordType keyword)
    {
        var hearing = new NpcHearingService(_npcScripts, _fixture.Sectors);
        await _fixture.Network.ExecuteOnLoopAsync(() => hearing.Heard(_aria, text, [(int)keyword]));
    }

    private static string ShippedScript(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "moongate_root", "scripts", relativePath);
    }
}
