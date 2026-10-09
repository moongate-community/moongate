using DryIoc;
using Lua;
using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Server.Ultima.Types.Stable;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Stable;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.Vendors;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Npcs;

/// <summary>
///     The shipped <c>scripts/mobiles/stablemaster.lua</c>, the gump <c>stable_claim</c> it opens and its script, on the
///     real Lua engine and the real modules, with a recording stable service: an animal trainer at 1600,1600 and a
///     player three tiles east of it.
/// </summary>
public sealed class StablemasterScriptIntegrationTests : IAsyncLifetime
{
    private const int PromptCliloc = 1042558;
    private const int StabledCliloc = 1049677;
    private const int CannotStableCliloc = 1048053;
    private const int NotYoursCliloc = 1042562;
    private const int FullCliloc = 1042565;
    private const int NoGoldCliloc = 1042556;
    private const int NoPetsCliloc = 502671;
    private const int ListCliloc = 502100;
    private const int HandedCliloc = 1042559;
    private const int TooFarCliloc = 500446;
    private const int StableEntry = 3006126;
    private const int ClaimAllEntry = 3006127;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly StubDeathService _death = new();
    private readonly RecordingStableService _stable = new();
    private readonly StubTargetService _targets = new();
    private readonly RecordingVendorService _vendors = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private readonly MobileTemplateService _templates = new(
        new StubDataLoaderService().With(
            new MobileTemplate { Id = "m_animaltrainer", ScriptId = "stablemaster" },
            new MobileTemplate { Id = "horse", Name = "a horse", Tags = new() { [MountProps.MountItemTag] = "horse4" } }
        )
    );

    private readonly MobileEntity _trainer = new()
    {
        Id = new Serial(0x100), Name = "Brin", TemplateId = "m_animaltrainer", Map = MapType.Trammel,
        Location = new Point3D(1600, 1600, 0), Direction = DirectionType.North
    };

    private readonly MobileEntity _horse = new()
    {
        Id = new Serial(0x101), Name = "a horse", TemplateId = "horse", Map = MapType.Trammel,
        Location = new Point3D(1603, 1601, 0)
    };

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private LuaScriptEngineService _engine = null!;
    private NpcScriptService _npcs = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.AccountId = new Serial(0x42);
        _aria.Body = 0x0190;
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1603, 1600, 0)));
        _fixture.Mobiles.EnterWorld(_trainer);
        _fixture.Mobiles.EnterWorld(_horse);
        GumpScriptService? gumpScripts = null;
        var root = Path.Combine(RepositoryRoot(), "moongate_root");
        var gumpTemplates =
            (await new GumpsLoader(new DirectoriesConfig(root, ["templates"])).LoadDataAsync()).Entities.ToArray();
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISectorService>(_fixture.Sectors);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileStateService>(new RecordingMobileStateService { Apply = true });
        _container.RegisterInstance<ITeleportService>(new RecordingTeleportService());
        _container.RegisterInstance<ICrimeService>(new RecordingCrimeService());
        _container.RegisterInstance<IDeathService>(_death);
        _container.RegisterInstance<IMobileTemplateService>(_templates);
        _container.RegisterInstance<ILineOfSightService>(new StubLineOfSightService());
        _container.RegisterInstance<IPathfindingService>(new StubPathfindingService());
        _container.RegisterInstance<IMovementService>(new StubMovementService());
        _container.RegisterInstance<IItemService>(TestItems.Create(_fixture.Sectors));
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With(gumpTemplates), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
        _container.RegisterInstance<TimeProvider>(new SettableClock());
        _container.RegisterInstance<ITargetService>(_targets);
        _container.RegisterInstance<IStableService>(_stable);
        _container.RegisterInstance(new StableConfig());
        _container.RegisterDelegate<IScriptEngine>(_ => _engine);
        _container.AddScriptModule<NpcModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<GumpModule>();
        _container.AddScriptModule<TargetModule>();
        _container.AddScriptModule<StableModule>();
        _container.RegisterInstance<IVendorService>(_vendors);
        _container.RegisterInstance<ITrainingService>(new RecordingTrainingService());
        _container.AddScriptModule<VendorModule>();
        _container.AddScriptModule<TrainerModule>();
        _container.RegisterScriptEnum<SpeechKeywordType>();
        _container.RegisterScriptEnum<StableResultType>();
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );

        foreach (var script in new[]
                     { "mobiles/stablemaster.lua", "common/shop.lua", "common/training.lua", "gumps/stable_claim.lua" })
        {
            _scripts.Write(script, await File.ReadAllTextAsync(Path.Combine(root, "scripts", script)));
        }

        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };
        _engine = new(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
        await _engine.StartAsync();
        gumpScripts = new GumpScriptService(_engine, _loop, options);
        await gumpScripts.StartAsync();
        _npcs = new(_engine, _templates, _loop, new ScriptEngineOptions { ScriptsDirectory = _scripts.Path });
        await _npcs.StartAsync();
        // A cursor answered inside a script is answered on the next turn of the loop, as the real one does.
        _loop.DeferTryPost = true;
    }

    [Fact]
    public void SayingStable_AsksForAPet_AndTheStabledPetIsAnswered()
    {
        _targets.Result = Moongate.Server.Ultima.Data.Targeting.TargetResult.ForObject(_horse.Id);

        Hear("stable", SpeechKeywordType.Stable);

        Assert.Empty(_errors);
        Assert.Equal(PromptCliloc, Assert.Single(_speech.ToldClilocs).Cliloc);
        Assert.Equal((_aria, _horse), Assert.Single(_stable.Stables));
        Assert.Equal(StabledCliloc, Assert.Single(_speech.SaidClilocs).Cliloc);
    }

    [Theory,
     InlineData(StableResultType.NotAPet, CannotStableCliloc),
     InlineData(StableResultType.Failed, CannotStableCliloc),
     InlineData(StableResultType.Dying, CannotStableCliloc),
     InlineData(StableResultType.NotYours, NotYoursCliloc),
     InlineData(StableResultType.Full, FullCliloc),
     InlineData(StableResultType.NoGold, NoGoldCliloc),
     InlineData(StableResultType.TooFar, TooFarCliloc)]
    public void ARefusedPet_IsAnsweredWithTheClientsText(StableResultType result, int cliloc)
    {
        _stable.Result = result;
        _targets.Result = Moongate.Server.Ultima.Data.Targeting.TargetResult.ForObject(_horse.Id);

        Hear("stable", SpeechKeywordType.Stable);

        Assert.Empty(_errors);
        Assert.Equal(cliloc, Assert.Single(_speech.SaidClilocs).Cliloc);
    }

    [Fact]
    public void ACanceledCursor_StablesNothing()
    {
        Hear("stable", SpeechKeywordType.Stable);

        Assert.Empty(_stable.Stables);
        Assert.Empty(_speech.SaidClilocs);
    }

    [Fact]
    public void SayingStable_FromFarAway_OrAsAGhost_IsNotHeard()
    {
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1640, 1600, 0)));
        Hear("stable", SpeechKeywordType.Stable);
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1603, 1600, 0)));
        _aria.Body = 0x0192;
        Hear("stable", SpeechKeywordType.Stable);

        Assert.Empty(_speech.ToldClilocs);
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public void SayingClaim_WithNoPets_SaysThereAreNone()
    {
        Hear("claim", SpeechKeywordType.Claim);

        Assert.Empty(_errors);
        Assert.Equal(NoPetsCliloc, Assert.Single(_speech.SaidClilocs).Cliloc);
        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public void SayingClaim_WithPets_OpensTheListAndAButtonClaimsThatPet()
    {
        _stable.Pets.AddRange(["horse", "llama"]);

        Hear("claim", SpeechKeywordType.Claim);
        Answer(2);

        Assert.Empty(_errors);
        Assert.Equal(ListCliloc, _speech.SaidClilocs[0].Cliloc);
        Assert.Equal("stable_claim", Assert.Single(_gumps.Opened).Gump.Id);
        Assert.Equal((_aria, 1, "llama"), Assert.Single(_stable.Claims));
        Assert.Equal(HandedCliloc, _speech.SaidClilocs[1].Cliloc);
    }

    [Fact]
    public void ThePetsOfTheList_AreShownByTheNameOfTheirTemplate()
    {
        _stable.Pets.AddRange(["horse", "vanished"]);

        Hear("claim", SpeechKeywordType.Claim);

        var texts = _gumps.Opened[0].Gump.Layout.Entries.OfType<GumpLabelCropped>().Select(label => label.Text).ToList();
        Assert.Contains("a horse", texts);
        Assert.Contains("vanished", texts);
    }

    [Fact]
    public void AButtonOfAListThatChanged_ReopensTheList()
    {
        _stable.Pets.Add("horse");
        Hear("claim", SpeechKeywordType.Claim);
        _stable.Result = StableResultType.BadIndex;

        Answer(1);

        Assert.Equal(2, _gumps.Opened.Count);
        Assert.DoesNotContain(_speech.SaidClilocs, said => said.Cliloc == HandedCliloc);
    }

    [Fact]
    public void AButtonPressedFromFarAway_ClaimsNothing()
    {
        _stable.Pets.Add("horse");
        Hear("claim", SpeechKeywordType.Claim);
        Assert.True(_fixture.Mobiles.MoveTo(_aria, MapType.Trammel, new Point3D(1640, 1600, 0)));

        Answer(1);

        Assert.Empty(_stable.Claims);
        Assert.Contains(_speech.ToldClilocs, told => told.Cliloc == TooFarCliloc);
    }

    [Fact]
    public void TheContextMenu_OffersStableAndClaimAll_AndStableAsksForAPet()
    {
        _targets.Result = Moongate.Server.Ultima.Data.Targeting.TargetResult.ForObject(_horse.Id);

        var menu = _npcs.Run(_trainer, "on_context_menu", (long)_aria.Id.Value);
        _npcs.Run(_trainer, "on_context_menu_select", (long)_aria.Id.Value, "stable");
        _loop.RunDeferred();

        Assert.Empty(_errors);
        var entries = Assert.IsType<LuaTable>(Assert.Single(menu.Values));
        var cliloc = Enumerable.Range(1, entries.ArrayLength)
            .Select(index => (int)entries[index].Read<LuaTable>()["cliloc"].Read<double>())
            .ToList();
        Assert.Contains(StableEntry, cliloc);
        Assert.Contains(ClaimAllEntry, cliloc);
        Assert.Equal((_aria, _horse), Assert.Single(_stable.Stables));
    }

    [Fact]
    public void ClaimAll_ClaimsEveryPetFromTheFirstPlace()
    {
        _stable.Pets.AddRange(["horse", "llama"]);

        _npcs.Run(_trainer, "on_context_menu_select", (long)_aria.Id.Value, "claim_all");

        Assert.Empty(_errors);
        Assert.Equal(2, _stable.Claims.Count);
        Assert.All(_stable.Claims, claim => Assert.Equal(0, claim.Index));
        Assert.Equal(HandedCliloc, _speech.SaidClilocs[^1].Cliloc);
    }

    [Fact]
    public void AGhost_ClaimsNothingAndStablesNothing_FromTheMenu()
    {
        _stable.Pets.Add("horse");
        _aria.Body = 0x0192;
        _targets.Result = Moongate.Server.Ultima.Data.Targeting.TargetResult.ForObject(_horse.Id);

        _npcs.Run(_trainer, "on_context_menu_select", (long)_aria.Id.Value, "claim_all");
        _npcs.Run(_trainer, "on_context_menu_select", (long)_aria.Id.Value, "stable");
        _loop.RunDeferred();

        Assert.Empty(_errors);
        Assert.Empty(_stable.Claims);
        Assert.Empty(_stable.Stables);
        Assert.Empty(_speech.SaidClilocs);
    }

    [Fact]
    public void ClaimAll_WithNoPets_SaysThereAreNone()
    {
        _npcs.Run(_trainer, "on_context_menu_select", (long)_aria.Id.Value, "claim_all");

        Assert.Equal(NoPetsCliloc, Assert.Single(_speech.SaidClilocs).Cliloc);
    }

    private void Hear(string text, SpeechKeywordType keyword)
    {
        new NpcHearingService(_npcs, _fixture.Sectors).Heard(_aria, text, [(int)keyword]);
        _loop.RunDeferred();
    }

    private void Answer(int button)
    {
        _gumps.Opened[0]
            .Gump.OnResponse(
                _session,
                new GumpResponse { ButtonId = button, Switches = new HashSet<int>(), Texts = new Dictionary<int, string>() }
            );
        _loop.RunDeferred();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }
}
