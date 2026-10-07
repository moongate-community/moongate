using Lua;
using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Server.Ultima.Types.Bank;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Npcs;

public sealed class NpcScriptIntegrationTests : IDisposable
{
    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly StubBankService _bank = new();
    private readonly ItemService _items = TestItems.Create();
    private readonly SettableClock _clock = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly List<LuaScriptEngineService> _engines = [];
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly MobileService _mobiles;

    private readonly MobileTemplateService _templates = new(
        new StubDataLoaderService().With(
            // The UOX3 cat's sounds, which the npc module plays by kind.
            new MobileTemplate
            {
                Id = "cat", ScriptId = "greeter", Sounds = new MobileSounds { StartAttack = 105, Idle = 675 }
            }
        )
    );

    private readonly MobileEntity _cat = new()
    {
        Id = new Serial(0x100), Name = "a cat", TemplateId = "cat", Map = MapType.Trammel,
        Location = new Point3D(1600, 1600, 0), Direction = DirectionType.North
    };

    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel,
        Location = new Point3D(1605, 1600, 0)
    };

    public NpcScriptIntegrationTests()
    {
        _mobiles = new(new StubMovementService(), _sectors);
        _mobiles.EnterWorld(_cat);
        _mobiles.EnterWorld(_aria);
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IMobileService>(_mobiles);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileTemplateService>(_templates);
        _container.AddScriptModule<NpcModule>();
        _container.AddScriptModule<DiceModule>();
        _container.RegisterInstance<IBankService>(_bank);
        _container.RegisterInstance<IItemService>(_items);
        _container.AddScriptModule<BankModule>();
        _container.RegisterInstance<TimeProvider>(_clock);
        _container.RegisterInstance<ITeleportService>(new RecordingTeleportService());
        _container.RegisterInstance<ICrimeService>(new RecordingCrimeService());
        _container.AddScriptModule<MobileModule>();
        _container.RegisterScriptEnum<BankResultType>();
        _container.RegisterScriptEnum<SpeechKeywordType>();
        _container.RegisterScriptEnum<SpeechType>();
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );
    }

    [Fact]
    public async Task AThink_RunsTheTemplateScriptWhichActsThroughTheNpcModule()
    {
        _scripts.Write(
            "mobiles/greeter.lua",
            """
            greeter = {}

            function greeter.on_think(serial)
                npc.say(serial, "Meow, I am " .. npc.name(serial))
                npc.step(serial, DirectionType.North)
            end
            """
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = NewScripts(engine);
        await scripts.StartAsync();

        scripts.Think(_cat);

        Assert.Empty(_errors);
        Assert.Equal((_cat, "Meow, I am a cat"), Assert.Single(_speech.Said));
        Assert.Equal(new Point3D(1600, 1599, 0), _cat.Location);
    }

    [Fact]
    public async Task APlayerSpeaking_RunsOnSpeechWhichMayAnswerLater()
    {
        _scripts.Write(
            "mobiles/greeter.lua",
            """
            greeter = {}

            function greeter.on_speech(serial, speaker, text)
                if text:lower():find("hello", 1, true) then
                    wait(1)
                    npc.say(serial, "Hello to you, " .. speaker)
                end
            end
            """
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = NewScripts(engine);
        await scripts.StartAsync();

        new NpcHearingService(scripts, _sectors).Heard(_aria, "HELLO cat");
        Assert.Empty(_speech.Said);
        _timers.Fire(_timers.Timers.Single().Id);

        Assert.Empty(_errors);
        Assert.Equal("Hello to you, 2", Assert.Single(_speech.Said).Text);
    }

    [Fact]
    public async Task AWhisperOrAYell_IsHeardByTheNpcsWithinItsOwnRange_AndOnSpeechGetsItsType()
    {
        _scripts.Write(
            "mobiles/greeter.lua",
            """
            greeter = {}

            function greeter.on_speech(serial, speaker, text, keywords, type)
                if type == SpeechType.Whisper then
                    npc.say(serial, "a whisper")
                elseif type == SpeechType.Yell then
                    npc.say(serial, "a yell")
                else
                    npc.say(serial, "speech " .. type)
                end
            end
            """
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = NewScripts(engine);
        await scripts.StartAsync();
        var hearing = new NpcHearingService(scripts, _sectors);

        // The cat is five cells from Aria: too far for a whisper.
        hearing.Heard(_aria, "psst", null, SpeechType.Whisper);
        Assert.Empty(_speech.Said);

        hearing.Heard(_aria, "hey", null, SpeechType.Yell);
        hearing.Heard(_aria, "hello");
        _aria.Location = new Point3D(1601, 1600, 0);
        hearing.Heard(_aria, "psst", null, SpeechType.Whisper);
        _aria.Location = new Point3D(1619, 1600, 0);
        hearing.Heard(_aria, "hey", null, SpeechType.Yell);

        Assert.Empty(_errors);
        Assert.Equal(["a yell", "speech 0", "a whisper"], _speech.Said.Select(said => said.Text));
    }

    [Fact]
    public async Task TheShippedWanderScript_StepsEveryFourthThinkAndAnswersAGreeting()
    {
        _scripts.Write("mobiles/wander.lua", File.ReadAllText(ShippedScript("mobiles/wander.lua")));
        var templates = new MobileTemplateService(
            new StubDataLoaderService().With(new MobileTemplate { Id = "cat", ScriptId = "wander" })
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = new NpcScriptService(
            engine,
            templates,
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();

        for (var think = 0; think < 3; think++)
        {
            scripts.Think(_cat);
        }

        Assert.Empty(_view.Calls);
        scripts.Think(_cat);
        Assert.Single(_view.Calls);

        new NpcHearingService(scripts, _sectors).Heard(_aria, "Hello!");
        _timers.Fire(_timers.Timers.Single().Id);

        Assert.Empty(_errors);
        Assert.Equal("Well met, traveller.", Assert.Single(_speech.Said).Text);
    }

    [Fact]
    public async Task TheShippedWanderScript_KeepsASpawnedNpcInItsHomeArea()
    {
        SetHome(1600, 1600, 1601, 1600);
        var scripts = await StartWanderAsync();

        for (var think = 0; think < 40; think++)
        {
            scripts.Think(_cat);
        }

        Assert.Empty(_errors);
        Assert.Equal(10, _view.Calls.Count);
        Assert.InRange(_cat.Location.X, 1600, 1601);
        Assert.Equal(1600, _cat.Location.Y);
    }

    [Fact]
    public async Task TheShippedWanderScript_LeavesAnNpcWithAOneCellHomeWhereItIs()
    {
        SetHome(1600, 1600, 1600, 1600);
        var scripts = await StartWanderAsync();

        for (var think = 0; think < 8; think++)
        {
            scripts.Think(_cat);
        }

        Assert.Empty(_errors);
        Assert.Empty(_view.Calls);
    }

    [Fact]
    public async Task TheShippedWanderScript_WalksAnNpcOutsideItsHomeAreaBackToIt()
    {
        SetHome(1610, 1590, 1612, 1592);
        var scripts = await StartWanderAsync();

        for (var think = 0; think < 4; think++)
        {
            scripts.Think(_cat);
        }

        Assert.Empty(_errors);
        Assert.Equal(new Point3D(1601, 1599, 0), _cat.Location);
    }

    [Fact]
    public async Task AMobileScriptWithASyntaxError_IsReportedAndTheServerStartsWithTheOthers()
    {
        _scripts.Write("mobiles/broken.lua", "broken = {} function broken.on_think(serial) npc.say(serial, end");
        _scripts.Write("mobiles/greeter.lua", "greeter = {} function greeter.on_think(serial) npc.say(serial, 'ok') end");
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = NewScripts(engine);

        await scripts.StartAsync();
        scripts.Think(_cat);

        Assert.Single(_errors);
        Assert.Equal("ok", Assert.Single(_speech.Said).Text);
    }

    [Theory, InlineData("orione"), InlineData("vega")]
    public async Task TheShippedCatScripts_TalkAndMeowWithoutErrors(string cat)
    {
        _scripts.Write($"mobiles/{cat}.lua", File.ReadAllText(ShippedScript($"mobiles/{cat}.lua")));
        var templates = new MobileTemplateService(
            new StubDataLoaderService().With(
                new MobileTemplate { Id = "cat", ScriptId = cat }
            )
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = new NpcScriptService(
            engine,
            templates,
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();

        for (var think = 0; think < 12; think++)
        {
            scripts.Think(_cat);
        }

        Assert.Empty(_errors);
        Assert.Equal(3, _speech.Said.Count);
        Assert.All(_speech.Said, said => Assert.StartsWith("M", said.Text));
        Assert.Equal(2, _speech.Sounds.Count);
        Assert.All(_speech.Sounds, sound => Assert.Contains(sound.Sound, new[] { 105, 675 }));
    }

    [Fact]
    public async Task TheShippedVegaScript_CountsTheHellosInAProp()
    {
        _scripts.Write("mobiles/vega.lua", File.ReadAllText(ShippedScript("mobiles/vega.lua")));
        var templates = new MobileTemplateService(
            new StubDataLoaderService().With(new MobileTemplate { Id = "cat", ScriptId = "vega" })
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = new NpcScriptService(
            engine,
            templates,
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();
        _cat.SetProp("vega.greeted", 4L);
        var hearing = new NpcHearingService(scripts, _sectors);

        hearing.Heard(_aria, "hello Vega");
        hearing.Heard(_aria, "Hello again");

        Assert.Empty(_errors);
        Assert.Equal(["Meow! That's 5 hellos.", "Meow! That's 6 hellos."], _speech.Said.Select(said => said.Text));
        Assert.Equal(6L, _cat.GetProp<long>("vega.greeted"));
    }

    [Fact]
    public async Task TheShippedBankerScript_OpensTheBankOnTheBankKeywordInAnyLanguage_OrTheWordBank()
    {
        _scripts.Write("mobiles/banker.lua", File.ReadAllText(ShippedScript("mobiles/banker.lua")));
        _scripts.Write("common/numbers.lua", File.ReadAllText(ShippedScript("common/numbers.lua")));
        var templates = new MobileTemplateService(
            new StubDataLoaderService().With(new MobileTemplate { Id = "cat", ScriptId = "banker" })
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = new NpcScriptService(
            engine,
            templates,
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();
        var hearing = new NpcHearingService(scripts, _sectors);

        hearing.Heard(_aria, "apri la banca", [(int)SpeechKeywordType.Bank]);
        // A moment later: within the same moment the bankers take the words as one request.
        _clock.Advance(TimeSpan.FromSeconds(1));
        hearing.Heard(_aria, "Bank, please");
        _clock.Advance(TimeSpan.FromSeconds(1));
        hearing.Heard(_aria, "hello");

        Assert.Empty(_errors);
        Assert.Equal([_aria, _aria], _bank.Opened);
    }

    [Fact]
    public async Task TheShippedBankerScript_TurnsToWhoAsksForTheBank_NotToWhoSaysAnythingElse()
    {
        // Every NPC is born facing south: one that never walks would stay so.
        _scripts.Write("mobiles/banker.lua", File.ReadAllText(ShippedScript("mobiles/banker.lua")));
        _scripts.Write("common/numbers.lua", File.ReadAllText(ShippedScript("common/numbers.lua")));
        var templates = new MobileTemplateService(
            new StubDataLoaderService().With(new MobileTemplate { Id = "cat", ScriptId = "banker" })
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = new NpcScriptService(
            engine,
            templates,
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();
        var hearing = new NpcHearingService(scripts, _sectors);

        hearing.Heard(_aria, "nice weather");
        Assert.Equal(DirectionType.North, _cat.Direction);

        hearing.Heard(_aria, "bank");

        Assert.Empty(_errors);
        // Aria stands to the east.
        Assert.Equal(DirectionType.East, _cat.Direction);
    }

    [Theory, InlineData("wander"), InlineData("vega")]
    public async Task TheShippedScriptsThatAnswerAGreeting_TurnToWhoGreets(string script)
    {
        _scripts.Write($"mobiles/{script}.lua", File.ReadAllText(ShippedScript($"mobiles/{script}.lua")));
        var templates = new MobileTemplateService(
            new StubDataLoaderService().With(new MobileTemplate { Id = "cat", ScriptId = script })
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = new NpcScriptService(
            engine,
            templates,
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();

        new NpcHearingService(scripts, _sectors).Heard(_aria, "Hello!");

        Assert.Empty(_errors);
        Assert.Equal(DirectionType.East, _cat.Direction);
    }

    [Fact]
    public async Task TheBanker_TellsTheBalance_WithItsThousands()
    {
        var hearing = await StartBankerAsync();
        _bank.Gold[_aria.Id] = 1_234_567;

        hearing.Heard(_aria, "balance", [(int)SpeechKeywordType.Balance]);

        Assert.Empty(_errors);
        Assert.Equal((_cat, 1042759, "1,234,567"), Assert.Single(_speech.SaidClilocs));
    }

    [Theory,
     InlineData("withdraw 500", 500),
     InlineData("I wish to withdraw 500 gold", 500),
     InlineData("500 withdraw", 500),
     InlineData("prelevo 1200", 1200)]
    public async Task TheBanker_Withdraws_TheFirstNumberOfTheSentence(string said, int amount)
    {
        var hearing = await StartBankerAsync();
        _bank.Gold[_aria.Id] = 5000;

        hearing.Heard(_aria, said, [(int)SpeechKeywordType.Withdraw]);

        Assert.Empty(_errors);
        Assert.Equal([(_aria, amount)], _bank.Withdrawn);
        Assert.Equal((_cat, 1010005, ""), Assert.Single(_speech.SaidClilocs));
    }

    [Theory,
     InlineData(BankResultType.TooMuch, 500381),
     InlineData(BankResultType.NotEnoughGold, 500384),
     InlineData(BankResultType.NoBank, 500384),
     InlineData(BankResultType.BackpackFull, 1048147)]
    public async Task TheBanker_RefusesAWithdrawal_WithTheWordsOfTheClient(BankResultType refusal, int cliloc)
    {
        var hearing = await StartBankerAsync();
        _bank.Result = refusal;

        hearing.Heard(_aria, "withdraw 500", [(int)SpeechKeywordType.Withdraw]);

        Assert.Empty(_errors);
        Assert.Equal((_cat, cliloc, ""), Assert.Single(_speech.SaidClilocs));
    }

    // No amount, none above zero, or one no bank could hold: the banker says nothing, and no gold moves.
    [Theory,
     InlineData("withdraw"),
     InlineData("withdraw 0"),
     InlineData("withdraw 99999999999"),
     InlineData("withdraw some gold"),
     // Digits of another script are no amount, and must not stop the script.
     InlineData("withdraw \u0665\u0660\u0660")]
    public async Task TheBanker_HearsNoAmount_AndDoesNothing(string said)
    {
        var hearing = await StartBankerAsync();
        _bank.Gold[_aria.Id] = 5000;

        hearing.Heard(_aria, said, [(int)SpeechKeywordType.Withdraw]);

        Assert.Empty(_errors);
        Assert.Empty(_bank.Withdrawn);
        Assert.Empty(_speech.SaidClilocs);
    }

    [Theory,
     InlineData("deposit 300"),
     InlineData("I would deposit 300 coins"),
     InlineData("DEPOSIT 300"),
     // The word at the very end of the sentence.
     InlineData("300 deposit"),
     InlineData("300 gold, to deposit")]
    public async Task TheBanker_Deposits_OnTheWordDeposit(string said)
    {
        var hearing = await StartBankerAsync();

        hearing.Heard(_aria, said);

        Assert.Empty(_errors);
        Assert.Equal([(_aria, 300)], _bank.Deposited);
        Assert.Equal((_cat, 1042763, "300"), Assert.Single(_speech.SaidClilocs));
    }

    // The word alone, not a word that holds it.
    [Fact]
    public async Task TheBanker_DoesNotDeposit_OnAWordThatHoldsDeposit()
    {
        var hearing = await StartBankerAsync();

        hearing.Heard(_aria, "the depository has 300 crates");

        Assert.Empty(_bank.Deposited);
        Assert.Empty(_speech.SaidClilocs);
    }

    [Theory, InlineData(BankResultType.NotEnoughGold, 500384), InlineData(BankResultType.BankFull, 500390)]
    public async Task TheBanker_RefusesADeposit_WithTheWordsOfTheClient(BankResultType refusal, int cliloc)
    {
        var hearing = await StartBankerAsync();
        _bank.Result = refusal;

        hearing.Heard(_aria, "deposit 300");

        Assert.Equal((_cat, cliloc, ""), Assert.Single(_speech.SaidClilocs));
    }

    // A player who never opened its bank has no box to deposit into: the banker opens it, and the player asks again.
    [Fact]
    public async Task TheBanker_OpensTheBank_OfWhoDepositsWithoutHavingOne()
    {
        var hearing = await StartBankerAsync();
        _bank.Result = BankResultType.NoBank;

        hearing.Heard(_aria, "deposit 300");

        Assert.Equal([_aria], _bank.Opened);
        Assert.Empty(_speech.SaidClilocs);
    }

    [Theory,
     InlineData("bank", SpeechKeywordType.Bank, 500378),
     InlineData("balance", SpeechKeywordType.Balance, 500389),
     InlineData("withdraw 500", SpeechKeywordType.Withdraw, 500389)]
    public async Task TheBanker_DoesNoBusinessWithACriminal(string said, SpeechKeywordType keyword, int cliloc)
    {
        var hearing = await StartBankerAsync();
        _bank.Gold[_aria.Id] = 5000;
        _aria.Criminal = true;

        hearing.Heard(_aria, said, [(int)keyword]);
        _clock.Advance(TimeSpan.FromSeconds(1));
        hearing.Heard(_aria, "deposit 300");

        Assert.Empty(_errors);
        Assert.Equal([(_cat, cliloc, ""), (_cat, 500389, "")], _speech.SaidClilocs);
        Assert.Empty(_bank.Opened);
        Assert.Empty(_bank.Withdrawn);
        Assert.Empty(_bank.Deposited);
    }

    // Two bankers behind one counter hear the same words: the gold moves once.
    [Fact]
    public async Task TwoBankersInRange_AnswerOnce()
    {
        var hearing = await StartBankerAsync();
        _mobiles.EnterWorld(
            new MobileEntity
            {
                Id = new Serial(0x101), Name = "a teller", TemplateId = "cat", Map = MapType.Trammel,
                Location = new Point3D(1601, 1600, 0)
            }
        );
        _bank.Gold[_aria.Id] = 5000;

        hearing.Heard(_aria, "withdraw 500", [(int)SpeechKeywordType.Withdraw]);

        Assert.Empty(_errors);
        Assert.Equal([(_aria, 500)], _bank.Withdrawn);
        Assert.Single(_speech.SaidClilocs);
    }

    [Fact]
    public async Task TheBanker_DoesNotHearWhoStandsFartherThanTwelveTiles()
    {
        var hearing = await StartBankerAsync();
        _aria.Location = new Point3D(1613, 1600, 0);
        _bank.Gold[_aria.Id] = 5000;

        hearing.Heard(_aria, "withdraw 500", [(int)SpeechKeywordType.Withdraw]);
        hearing.Heard(_aria, "bank", [(int)SpeechKeywordType.Bank]);

        Assert.Empty(_bank.Withdrawn);
        Assert.Empty(_bank.Opened);
        Assert.Empty(_speech.SaidClilocs);
    }

    // A number of ten digits that is still an amount: the bank answers it like any other, here that it is too much.
    [Fact]
    public async Task TheBanker_AnswersAnAmountOfTenDigits()
    {
        var hearing = await StartBankerAsync();
        _bank.Result = BankResultType.TooMuch;

        hearing.Heard(_aria, "withdraw 1000000000", [(int)SpeechKeywordType.Withdraw]);

        Assert.Empty(_errors);
        Assert.Equal([(_aria, 1_000_000_000)], _bank.Withdrawn);
        Assert.Equal((_cat, 500381, ""), Assert.Single(_speech.SaidClilocs));
    }

    [Fact]
    public async Task TheBanker_WritesACheck_AndSaysItsAmountAfterTheTextOfTheClient()
    {
        var hearing = await StartBankerAsync();

        hearing.Heard(_aria, "check 125000", [(int)SpeechKeywordType.Check]);

        Assert.Empty(_errors);
        Assert.Equal([(_aria, 125_000)], _bank.Checks);
        Assert.Equal((_cat, 1042673, ""), Assert.Single(_speech.SaidClilocs));
        Assert.Equal(["125,000"], _speech.SaidAffixes);
    }

    [Theory,
     InlineData(BankResultType.CheckTooSmall, 1010006),
     InlineData(BankResultType.CheckTooBig, 1010007),
     InlineData(BankResultType.NotEnoughGold, 500384),
     InlineData(BankResultType.NoBank, 500384),
     InlineData(BankResultType.BankFull, 500386)]
    public async Task TheBanker_RefusesACheck_WithTheWordsOfTheClient(BankResultType refusal, int cliloc)
    {
        var hearing = await StartBankerAsync();
        _bank.Result = refusal;

        hearing.Heard(_aria, "check 5000", [(int)SpeechKeywordType.Check]);

        Assert.Empty(_errors);
        Assert.Equal((_cat, cliloc, ""), Assert.Single(_speech.SaidClilocs));
        Assert.Equal([""], _speech.SaidAffixes);
    }

    [Fact]
    public async Task TheBanker_WritesNoCheck_ForACriminal_OrWithoutAnAmount()
    {
        var hearing = await StartBankerAsync();

        hearing.Heard(_aria, "check", [(int)SpeechKeywordType.Check]);
        _clock.Advance(TimeSpan.FromSeconds(1));
        _aria.Criminal = true;
        hearing.Heard(_aria, "check 5000", [(int)SpeechKeywordType.Check]);

        Assert.Empty(_errors);
        Assert.Empty(_bank.Checks);
        Assert.Equal((_cat, 500389, ""), Assert.Single(_speech.SaidClilocs));
    }

    // Gold or a check dropped on the banker goes into the bank, and the banker says how much.
    [Fact]
    public async Task TheBanker_DepositsWhatIsDroppedOnIt_AndTakesIt()
    {
        var scripts = await StartBankerScriptsAsync();
        var gold = new ItemEntity { Id = new Serial(0x40000001), TemplateId = "gold", ItemId = 0x0EED, Amount = 1250 };
        _items.Add([gold]);
        _bank.ItemDeposits = 1250;

        var result = scripts.Run(_cat, "on_drag_drop", (long)_aria.Id.Value, (long)gold.Id.Value);

        Assert.Empty(_errors);
        Assert.Equal([true], result.Values);
        Assert.Equal((_aria, gold), Assert.Single(_bank.DepositedItems));
        Assert.Equal((_cat, 1042763, "1,250"), Assert.Single(_speech.SaidClilocs));
    }

    [Theory,
     InlineData(BankResultType.BankFull, 500390),
     InlineData(BankResultType.NotMoney, 501550)]
    public async Task TheBanker_GivesBackWhatItDoesNotDeposit_WithTheWordsOfTheClient(BankResultType refusal, int cliloc)
    {
        var scripts = await StartBankerScriptsAsync();
        var item = new ItemEntity { Id = new Serial(0x40000001), TemplateId = "sword", ItemId = 0x0F5E, Amount = 1 };
        _items.Add([item]);
        _bank.Result = refusal;

        var result = scripts.Run(_cat, "on_drag_drop", (long)_aria.Id.Value, (long)item.Id.Value);

        Assert.Empty(_errors);
        Assert.Equal([false], result.Values);
        Assert.Equal((_cat, cliloc, ""), Assert.Single(_speech.SaidClilocs));
    }

    [Fact]
    public async Task TheBanker_TakesNothingFromACriminal()
    {
        var scripts = await StartBankerScriptsAsync();
        var gold = new ItemEntity { Id = new Serial(0x40000001), TemplateId = "gold", ItemId = 0x0EED, Amount = 1250 };
        _items.Add([gold]);
        _aria.Criminal = true;

        var result = scripts.Run(_cat, "on_drag_drop", (long)_aria.Id.Value, (long)gold.Id.Value);

        Assert.Empty(_errors);
        Assert.Equal([false], result.Values);
        Assert.Empty(_bank.DepositedItems);
        Assert.Equal((_cat, 500389, ""), Assert.Single(_speech.SaidClilocs));
    }

    // No bank box yet: it is made and shown, and the player hands the gold again.
    [Fact]
    public async Task TheBanker_OpensTheBankOfWhoNeverHadOne_AndGivesTheItemBack()
    {
        var scripts = await StartBankerScriptsAsync();
        var gold = new ItemEntity { Id = new Serial(0x40000001), TemplateId = "gold", ItemId = 0x0EED, Amount = 1250 };
        _items.Add([gold]);
        _bank.Result = BankResultType.NoBank;

        var result = scripts.Run(_cat, "on_drag_drop", (long)_aria.Id.Value, (long)gold.Id.Value);

        Assert.Empty(_errors);
        Assert.Equal([false], result.Values);
        Assert.Equal([_aria], _bank.Opened);
        Assert.Empty(_speech.SaidClilocs);
    }

    // The banker's own entry of its context menu: the client's "Open Bank Box", from twelve tiles.
    [Fact]
    public async Task TheBanker_OffersToOpenTheBankBox_InItsContextMenu()
    {
        var scripts = await StartBankerScriptsAsync();

        var result = scripts.Run(_cat, "on_context_menu", (long)_aria.Id.Value);

        Assert.Empty(_errors);
        var entries = Assert.IsType<LuaTable>(Assert.Single(result.Values));
        Assert.Equal(1, entries.ArrayLength);
        var entry = entries[1].Read<LuaTable>();
        Assert.Equal(
            ("bank", 3006105, 12),
            (entry["id"].Read<string>(), entry["cliloc"].Read<int>(), entry["range"].Read<int>())
        );
    }

    [Fact]
    public async Task TheBanker_OpensTheBankOfWhoChoosesIt_AndRefusesACriminal()
    {
        var scripts = await StartBankerScriptsAsync();

        scripts.Run(_cat, "on_context_menu_select", (long)_aria.Id.Value, "bank");

        Assert.Empty(_errors);
        Assert.Equal([_aria], _bank.Opened);
        Assert.Empty(_speech.SaidClilocs);

        _aria.Criminal = true;
        scripts.Run(_cat, "on_context_menu_select", (long)_aria.Id.Value, "bank");

        Assert.Equal([_aria], _bank.Opened);
        Assert.Equal((_cat, 500378, ""), Assert.Single(_speech.SaidClilocs));
    }

    // An entry that is not the banker's does nothing.
    [Fact]
    public async Task TheBanker_IgnoresAnEntryItDidNotOffer()
    {
        var scripts = await StartBankerScriptsAsync();

        scripts.Run(_cat, "on_context_menu_select", (long)_aria.Id.Value, "sell");

        Assert.Empty(_errors);
        Assert.Empty(_bank.Opened);
    }

    private async Task<NpcHearingService> StartBankerAsync()
    {
        return new NpcHearingService(await StartBankerScriptsAsync(), _sectors);
    }

    private async Task<NpcScriptService> StartBankerScriptsAsync()
    {
        _scripts.Write("mobiles/banker.lua", File.ReadAllText(ShippedScript("mobiles/banker.lua")));
        _scripts.Write("common/numbers.lua", File.ReadAllText(ShippedScript("common/numbers.lua")));
        var templates = new MobileTemplateService(
            new StubDataLoaderService().With(new MobileTemplate { Id = "cat", ScriptId = "banker" })
        );
        var engine = NewEngine();
        _engines.Add(engine);
        await engine.StartAsync();
        var scripts = new NpcScriptService(
            engine,
            templates,
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();

        return scripts;
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

    private NpcScriptService NewScripts(IScriptEngine engine)
    {
        return new(engine, _templates, _loop, new ScriptEngineOptions { ScriptsDirectory = _scripts.Path });
    }

    private void SetHome(long x1, long y1, long x2, long y2)
    {
        _cat.SetProp("spawn.x1", x1);
        _cat.SetProp("spawn.y1", y1);
        _cat.SetProp("spawn.x2", x2);
        _cat.SetProp("spawn.y2", y2);
    }

    private async Task<NpcScriptService> StartWanderAsync()
    {
        _scripts.Write("mobiles/wander.lua", File.ReadAllText(ShippedScript("mobiles/wander.lua")));
        var templates = new MobileTemplateService(
            new StubDataLoaderService().With(new MobileTemplate { Id = "cat", ScriptId = "wander" })
        );
        var engine = NewEngine();
        _engines.Add(engine);
        await engine.StartAsync();
        var scripts = new NpcScriptService(
            engine,
            templates,
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();

        return scripts;
    }

    private LuaScriptEngineService NewEngine()
    {
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path,
            MaxInstructionsPerResume = 20_000,
            MaxInstructionsPerChunk = 100_000,
            HookInterval = 100,
            WriteDefinitions = false
        };

        return new(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
    }

    public void Dispose()
    {
        foreach (var engine in _engines)
        {
            engine.Dispose();
        }

        _container.Dispose();
        _scripts.Dispose();
    }
}
