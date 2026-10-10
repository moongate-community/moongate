using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Spells;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Effects;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Magic;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Mounts;
using Moongate.Tests.TestSupport.Ultima.Skills;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class SpellCastServiceTests : IAsyncLifetime
{
    private const int AshGraphic = 0x0F8C;
    private const int ArrowScroll = 0x1F32;
    private const int MagicArrow = 5;
    private const int Fireball = 18;
    private const int CreateFood = 2;
    private const int Recall = 32;

    private readonly RecordingTimerService _timers = new();
    private readonly SettableClock _clock = new();
    private readonly RecordingTargetService _targets = new();
    private readonly StubSpellScriptService _scripts = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingEffectService _effects = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly StubSkillService _skills = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly StubItemHandlingService _handling = new();
    private readonly StubSpellbookService _books = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _bran = null!;
    private ItemService _items = null!;
    private ItemEntity _backpack = null!;
    private ItemEntity _book = null!;
    private ItemEntity _ash = null!;
    private SpellCastService _casts = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        await _fixture.AddAsync(3);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        Assert.True(_fixture.Mobiles.TryGet(new Serial(3), out _bran!));
        _aria.AccountId = new Serial(0x42);
        (_aria.Mana, _aria.ManaMax, _aria.Location) = (20, 20, new Point3D(10, 10, 0));
        _bran.Location = new Point3D(12, 10, 0);
        _items = TestItems.Create(_fixture.Sectors);
        _backpack = new ItemEntity { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        _backpack.Equip(_aria.Id, LayerType.Backpack);
        _items.Add([_backpack]);
        _book = new ItemEntity { Id = new Serial(0x40000002), TemplateId = "spellbook", ItemId = 0x0EFA, Amount = 1 };
        _book.PutInContainer(_backpack.Id, new Point2D(5, 5));
        _ash = new ItemEntity { Id = new Serial(0x40000003), TemplateId = "0x0f8c_sulfurous_ash", ItemId = AshGraphic, Amount = 3 };
        _ash.PutInContainer(_backpack.Id, new Point2D(6, 6));
        _items.Add([_book, _ash]);
        _books.Carried.Add(_book);
        _books.Add(_book, MagicArrow);
        _books.Add(_book, Fireball);
        _books.Add(_book, CreateFood);
        _books.Add(_book, Recall);
        _scripts.Keys.UnionWith(["magic_arrow", "fireball", "create_food", "recall"]);

        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "0x0f8c_sulfurous_ash", ItemId = new Serial(AshGraphic) },
                new ItemTemplate { Id = "arrowscroll", ItemId = new Serial(ArrowScroll) }
            )
        );
        var catalog = new SpellCatalogService(
            new StubDataLoaderService().With(
                new SpellDefinition
                {
                    Id = MagicArrow, Key = "magic_arrow", Name = "Magic Arrow", Circle = 1, Mantra = "In Por Ylem",
                    Action = 17, Target = SpellTargetType.Mobile, Harmful = true, Scroll = "arrowscroll",
                    Reagents = [new SpellReagent { Template = "0x0f8c_sulfurous_ash", Amount = 1 }]
                },
                new SpellDefinition
                {
                    Id = Fireball, Key = "fireball", Name = "Fireball", Circle = 3, Mantra = "Vas Flam", Action = 17,
                    Target = SpellTargetType.Mobile, Harmful = true, Scroll = "fireballscroll",
                    Reagents = [new SpellReagent { Template = "0x0f8c_sulfurous_ash", Amount = 1 }]
                },
                new SpellDefinition
                {
                    Id = CreateFood, Key = "create_food", Name = "Create Food", Circle = 1, Mantra = "In Mani Yelm",
                    Action = 17, Target = SpellTargetType.None, Scroll = "createfoodscroll"
                },
                new SpellDefinition
                {
                    Id = Recall, Key = "recall", Name = "Recall", Circle = 4, Mantra = "Kal Ort Por", Action = 17,
                    Target = SpellTargetType.Item, Scroll = "recallscroll"
                }
            ),
            templates
        );
        _casts = new(
            catalog,
            _books,
            _scripts,
            _items,
            templates,
            _handling,
            _fixture.Mobiles,
            _state,
            _fixture.Sessions,
            _targets,
            _speech,
            _effects,
            _view,
            _skills,
            _sight,
            _timers,
            _clock
        );
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void CastFromBook_SaysTheWords_MakesTheGesture_AndRunsTheDelayOfTheCircle()
    {
        Assert.True(_casts.CastFromBook(_aria, MagicArrow));

        Assert.Equal("In Por Ylem", Assert.Single(_speech.Said).Text);
        Assert.Contains("Animated 2 17 7 1", _view.Calls);
        Assert.Equal(TimeSpan.FromSeconds(0.5), Assert.Single(_timers.Timers).Interval);
        Assert.True(_casts.IsCasting(_aria));
        Assert.True(_casts.BlocksMovement(_aria));
    }

    [Fact]
    public void CastFromBook_ACircleThreeSpell_TakesAThirdOfASecondMorePerCircle()
    {
        _casts.CastFromBook(_aria, Fireball);

        Assert.Equal(TimeSpan.FromSeconds(1.0), Assert.Single(_timers.Timers).Interval);
    }

    [Fact]
    public void CastFromBook_WithNoBookThatHoldsTheSpell_SaysYouDoNotHaveIt()
    {
        _books.Carried.Clear();

        Assert.False(_casts.CastFromBook(_aria, MagicArrow));
        Assert.False(_casts.CastFromBook(_aria, 63));

        Assert.Equal([500015, 500015], Told());
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void CastFromBook_TheBookTheRequestNamed_IsUsedWhenItHoldsTheSpell()
    {
        _books.Carried.Clear();
        _books.IsCarried = (_, _) => true;

        Assert.True(_casts.CastFromBook(_aria, MagicArrow, _book));
    }

    [Fact]
    public void CastFromBook_ADeadCaster_IsToldSo()
    {
        _aria.Body = 0x0192;

        Assert.False(_casts.CastFromBook(_aria, MagicArrow));

        Assert.Equal([1019048], Told());
    }

    [Fact]
    public void CastFromBook_WhileCasting_SaysItIsAlready_AndStartsNoSecond()
    {
        _casts.CastFromBook(_aria, MagicArrow);

        Assert.False(_casts.CastFromBook(_aria, Fireball));

        Assert.Equal([502642], Told());
        Assert.Single(_timers.Timers);
    }

    [Fact]
    public void CastFromBook_FrozenOrParalyzed_IsRefused()
    {
        _aria.Frozen = true;

        Assert.False(_casts.CastFromBook(_aria, MagicArrow));

        Assert.Equal([502643], Told());
    }

    [Fact]
    public void CastFromBook_WithoutTheMana_SaysSoOverTheCastersHead()
    {
        _aria.Mana = 3;

        Assert.False(_casts.CastFromBook(_aria, MagicArrow));

        var said = Assert.Single(_speech.SaidTo);
        Assert.Equal((502625, "4"), (said.Cliloc, said.Arguments));
        Assert.Equal((_aria, _aria), (said.Speaker, said.Player));
    }

    [Fact]
    public void CastFromBook_ASpellWithNoScript_OrOutOfTheGame_IsDisabled()
    {
        _scripts.Keys.Remove("magic_arrow");

        Assert.False(_casts.CastFromBook(_aria, MagicArrow));

        Assert.Equal([502345], Told());
    }

    [Fact]
    public void CastFromBook_ARevealedCaster_IsNoLongerHidden()
    {
        _aria.Hidden = true;

        _casts.CastFromBook(_aria, MagicArrow);

        Assert.False(_aria.Hidden);
    }

    [Fact]
    public void CastFromBook_Mounted_MakesNoGesture()
    {
        var mounts = new RecordingMountService();
        mounts.Mounted.Add(_aria.Id);
        var casts = new SpellCastService(
            CatalogOf(),
            _books,
            _scripts,
            _items,
            Templates(),
            _handling,
            _fixture.Mobiles,
            _state,
            _fixture.Sessions,
            _targets,
            _speech,
            _effects,
            _view,
            _skills,
            _sight,
            _timers,
            _clock,
            mounts
        );

        casts.CastFromBook(_aria, MagicArrow);

        Assert.DoesNotContain(_view.Calls, call => call.StartsWith("Animated", StringComparison.Ordinal));
    }

    [Fact]
    public void WhenTheDelayEnds_TheTargetCursorOfTheSpellComes_AndTheCasterMayMove()
    {
        _casts.CastFromBook(_aria, MagicArrow);

        FireDelay();

        Assert.Equal((TargetCursorType.Object, TargetFlagsType.Harmful), Assert.Single(_targets.Begun));
        Assert.False(_casts.BlocksMovement(_aria));
        Assert.True(_casts.IsCasting(_aria));
    }

    [Fact]
    public void TheTarget_PaysTheReagent_AndTheMana_ChecksMagery_AndRunsTheScript()
    {
        _casts.CastFromBook(_aria, MagicArrow);
        FireDelay();

        _targets.Answer(TargetResult.ForObject(_bran.Id));

        Assert.Equal(2, _ash.Amount);
        Assert.Equal(16, _aria.Mana);
        Assert.Equal((_aria, SkillType.Magery, 0.0, 40.0), Assert.Single(_skills.Checks));
        var cast = Assert.Single(_scripts.Casts);
        Assert.Equal(("magic_arrow", false), (cast.Key, cast.FromScroll));
        Assert.Equal((SpellTargetType.Mobile, _bran.Id), (cast.Target.Kind, cast.Target.Serial));
        Assert.False(_casts.IsCasting(_aria));
    }

    [Fact]
    public void ASpellOfTheThirdCircle_AsksTheWindowOfThatCircle_AndCostsItsMana()
    {
        _casts.CastFromBook(_aria, Fireball);
        FireDelay();

        _targets.Answer(TargetResult.ForObject(_bran.Id));

        Assert.Equal((_aria, SkillType.Magery, 20.0, 60.0), Assert.Single(_skills.Checks));
        Assert.Equal(11, _aria.Mana);
    }

    [Fact]
    public void ASpellWithNoTarget_TakesEffectWhenTheDelayEnds()
    {
        _casts.CastFromBook(_aria, CreateFood);

        FireDelay();

        Assert.Empty(_targets.Begun);
        var cast = Assert.Single(_scripts.Casts);
        Assert.Equal(SpellTargetType.None, cast.Target.Kind);
        Assert.Equal(16, _aria.Mana);
    }

    [Fact]
    public void AFizzle_LosesTheReagents_KeepsTheMana_AndShowsTheFizzle()
    {
        _skills.Result = false;
        _casts.CastFromBook(_aria, MagicArrow);
        FireDelay();

        _targets.Answer(TargetResult.ForObject(_bran.Id));

        Assert.Equal(2, _ash.Amount);
        Assert.Equal(20, _aria.Mana);
        Assert.Empty(_scripts.Casts);
        Assert.Equal(502632, Assert.Single(_speech.SaidTo).Cliloc);
        var effect = Assert.Single(_effects.On);
        Assert.Equal((_aria.Id, 0x3735), (effect.Target, effect.Options.Graphic));
        Assert.Equal(0x5C, Assert.Single(_speech.Sounds).Sound);
    }

    [Fact]
    public void WithoutTheReagents_SaysSo_SpendsNothing_AndDoesNotFizzle()
    {
        _items.Remove([_ash.Id]);
        _casts.CastFromBook(_aria, MagicArrow);
        FireDelay();

        _targets.Answer(TargetResult.ForObject(_bran.Id));

        Assert.Equal(502630, Assert.Single(_speech.SaidTo).Cliloc);
        Assert.Equal(20, _aria.Mana);
        Assert.Empty(_skills.Checks);
        Assert.Empty(_effects.On);
    }

    [Fact]
    public void TheManaBeingGoneByTheTarget_IsTold_AfterTheReagentsWereTaken()
    {
        _casts.CastFromBook(_aria, MagicArrow);
        FireDelay();
        _aria.Mana = 1;

        _targets.Answer(TargetResult.ForObject(_bran.Id));

        Assert.Equal(502625, Assert.Single(_speech.SaidTo).Cliloc);
        Assert.Empty(_skills.Checks);
        Assert.Equal(2, _ash.Amount);
    }

    [Fact]
    public void ACancelledCursor_SpendsNothing_AndEndsTheCast()
    {
        _casts.CastFromBook(_aria, MagicArrow);
        FireDelay();

        _targets.Answer(TargetResult.Canceled(TargetCancelType.Canceled));

        Assert.Equal((3, 20), (_ash.Amount, _aria.Mana));
        Assert.False(_casts.IsCasting(_aria));
        Assert.Empty(_scripts.Casts);
    }

    [Fact]
    public void ATargetTooFarOrOutOfSight_IsRefused_AndSpendsNothing()
    {
        _bran.Location = new Point3D(40, 10, 0);
        _casts.CastFromBook(_aria, MagicArrow);
        FireDelay();
        _targets.Answer(TargetResult.ForObject(_bran.Id));

        _bran.Location = new Point3D(12, 10, 0);
        _clock.Advance(TimeSpan.FromSeconds(1));
        _sight.Allow = false;
        _casts.CastFromBook(_aria, MagicArrow);
        FireDelay();
        _targets.Answer(TargetResult.ForObject(_bran.Id));

        Assert.Equal([500446, 500237], Told());
        Assert.Equal((3, 20), (_ash.Amount, _aria.Mana));
    }

    [Fact]
    public void AnItemSpell_TakesAnItemTheCasterCarries_AtTheCastersPlace()
    {
        var rune = new ItemEntity { Id = new Serial(0x40000020), TemplateId = "rune", ItemId = 0x1F14, Amount = 1 };
        rune.PutInContainer(_backpack.Id, new Point2D(3, 3));
        _items.Add([rune]);
        _casts.CastFromBook(_aria, Recall);
        FireDelay();

        _targets.Answer(TargetResult.ForObject(rune.Id));

        var cast = Assert.Single(_scripts.Casts);
        Assert.Equal(("recall", SpellTargetType.Item, rune.Id), (cast.Key, cast.Target.Kind, cast.Target.Serial));
        Assert.Equal(_aria.Location, cast.Target.Location);
    }

    [Fact]
    public void AnItemSpell_TakesAnItemOnTheGroundInRange_AndRefusesAMobile()
    {
        _aria.Mana = _aria.ManaMax = 40;
        var rune = new ItemEntity { Id = new Serial(0x40000020), TemplateId = "rune", ItemId = 0x1F14, Amount = 1 };
        _items.Add([rune]);
        _items.PlaceOnGround(rune, _aria.Map, new Point3D(11, 10, 0));
        _casts.CastFromBook(_aria, Recall);
        FireDelay();
        _targets.Answer(TargetResult.ForObject(rune.Id));

        Assert.Equal(new Point3D(11, 10, 0), Assert.Single(_scripts.Casts).Target.Location);

        _clock.Advance(TimeSpan.FromSeconds(1));
        _casts.CastFromBook(_aria, Recall);
        FireDelay();
        _targets.Answer(TargetResult.ForObject(_bran.Id));

        Assert.Equal([501857], Told());
    }

    [Fact]
    public void ACasterTargetingItself_NeedsNoSight()
    {
        _sight.Allow = false;
        _casts.CastFromBook(_aria, MagicArrow);
        FireDelay();

        _targets.Answer(TargetResult.ForObject(_aria.Id));

        Assert.Single(_scripts.Casts);
        Assert.Empty(_sight.Checks);
    }

    [Fact]
    public void TheScriptMayRefuseBeforeAnythingIsSpent_WithTheClientsText()
    {
        _scripts.Verdict = ScriptResult.Completed([1005000L]);
        _casts.CastFromBook(_aria, MagicArrow);
        FireDelay();

        _targets.Answer(TargetResult.ForObject(_bran.Id));

        Assert.Equal([1005000], Told());
        Assert.Equal((3, 20), (_ash.Amount, _aria.Mana));
        Assert.Empty(_scripts.Casts);
    }

    [Fact]
    public void AScroll_NeedsNoReagents_AsksAnEasierWindow_AndIsUsedUpOnSuccess()
    {
        var scroll = Scroll(ArrowScroll);
        _items.Remove([_ash.Id]);

        Assert.True(_casts.CastFromScroll(_aria, scroll));
        FireDelay();
        _targets.Answer(TargetResult.ForObject(_bran.Id));

        Assert.Equal((_aria, SkillType.Magery, -50.0, -10.0), Assert.Single(_skills.Checks));
        Assert.Equal((scroll, 1), Assert.Single(_handling.Consumed));
        var cast = Assert.Single(_scripts.Casts);
        Assert.True(cast.FromScroll);
        Assert.Equal(16, _aria.Mana);
    }

    [Fact]
    public void AScroll_ThatFizzles_StaysInThePack()
    {
        _skills.Result = false;
        var scroll = Scroll(ArrowScroll);
        _casts.CastFromScroll(_aria, scroll);
        FireDelay();

        _targets.Answer(TargetResult.ForObject(_bran.Id));

        Assert.Empty(_handling.Consumed);
        Assert.Equal(1, scroll.Amount);
        Assert.Equal(502632, Assert.Single(_speech.SaidTo).Cliloc);
    }

    [Fact]
    public void AScroll_ThatIsNotInThePack_SaysItMustBe()
    {
        var scroll = Scroll(ArrowScroll);
        scroll.PutInContainer(new Serial(0x40000050), new Point2D(1, 1));
        _items.Add([new ItemEntity { Id = new Serial(0x40000050), TemplateId = "chest", ItemId = 0x0E40, Amount = 1 }]);

        Assert.False(_casts.CastFromScroll(_aria, scroll));

        Assert.Equal([1042001], Told());
    }

    [Fact]
    public void AScrollGoneByTheTarget_Fizzles()
    {
        var scroll = Scroll(ArrowScroll);
        _casts.CastFromScroll(_aria, scroll);
        FireDelay();
        _items.Remove([scroll.Id]);

        _targets.Answer(TargetResult.ForObject(_bran.Id));

        Assert.Equal(502632, Assert.Single(_speech.SaidTo).Cliloc);
        Assert.Empty(_scripts.Casts);
    }

    [Fact]
    public void AfterTheDelay_TheNextCastWaitsThreeQuartersOfASecond()
    {
        _casts.CastFromBook(_aria, CreateFood);
        FireDelay();

        Assert.False(_casts.CastFromBook(_aria, CreateFood));
        Assert.Equal([502644], Told());

        _clock.Advance(TimeSpan.FromSeconds(0.74));
        Assert.False(_casts.CastFromBook(_aria, CreateFood));

        _clock.Advance(TimeSpan.FromSeconds(0.02));
        Assert.True(_casts.CastFromBook(_aria, CreateFood));
    }

    [Fact]
    public void Hurt_WhileACircleThreeSpellIsCast_RuinsIt_AndTheRecoveryIsTheLessOfTheDelayWasDone()
    {
        _casts.CastFromBook(_aria, Fireball);
        _clock.Advance(TimeSpan.FromSeconds(0.25));

        _casts.Hurt(_aria);

        Assert.Equal([500641], Told());
        Assert.False(_casts.IsCasting(_aria));
        Assert.Single(_timers.Unregistered);
        // 1 - sqrt(0.25 / 1.0) = 0.5 seconds.
        Assert.False(_casts.CastFromBook(_aria, CreateFood));
        _clock.Advance(TimeSpan.FromSeconds(0.5));
        Assert.True(_casts.CastFromBook(_aria, CreateFood));
    }

    [Fact]
    public void Hurt_NeverRuinsAFirstCircleSpell_NorOneWaitingForItsTarget()
    {
        _casts.CastFromBook(_aria, MagicArrow);

        _casts.Hurt(_aria);

        Assert.True(_casts.BlocksMovement(_aria));

        _casts.Cancel(_aria);
        _clock.Advance(TimeSpan.FromSeconds(1));
        _casts.CastFromBook(_aria, Fireball);
        FireDelay();

        _casts.Hurt(_aria);

        Assert.True(_casts.IsCasting(_aria));
        Assert.Empty(Told());
    }

    [Fact]
    public void Hurt_OfAnNpc_DoesNotDisturb()
    {
        _bran.Mana = 20;
        _casts.CastFromBook(_bran, Fireball);

        _casts.Hurt(_bran);

        Assert.True(_casts.IsCasting(_bran));
        Assert.True(_casts.BlocksMovement(_bran));
    }

    [Fact]
    public void Cancel_EndsTheCastAndTakesTheCursorAway()
    {
        _casts.CastFromBook(_aria, MagicArrow);
        FireDelay();

        _casts.Cancel(_aria);

        Assert.False(_casts.IsCasting(_aria));
        Assert.Equal(1, _targets.Cancels);
        Assert.Empty(Told());
    }

    [Fact]
    public void ClosingTheSession_EndsTheCast_AndItsTimers()
    {
        _casts.CastFromBook(_aria, MagicArrow);

        _casts.OnSessionClosed(_session);

        Assert.False(_casts.IsCasting(_aria));
        Assert.Single(_timers.Unregistered);
    }

    [Fact]
    public void ADelayThatEndsAfterTheCastWasEnded_DoesNothing()
    {
        _casts.CastFromBook(_aria, MagicArrow);
        var delay = _timers.Timers.Single();
        _casts.Cancel(_aria);

        delay.Callback();

        Assert.Empty(_targets.Begun);
    }

    private void FireDelay()
    {
        _timers.Fire(_timers.Timers.First(timer => timer.Name == "spell_cast").Id);
    }

    private List<int> Told()
    {
        return _speech.ToldClilocs.Where(told => told.Player == _aria).Select(told => told.Cliloc).ToList();
    }

    private ItemEntity Scroll(int graphic)
    {
        var scroll = new ItemEntity { Id = new Serial(0x40000010), TemplateId = "arrowscroll", ItemId = graphic, Amount = 1 };
        scroll.PutInContainer(_backpack.Id, new Point2D(9, 9));
        _items.Add([scroll]);

        return scroll;
    }

    private SpellCatalogService CatalogOf()
    {
        return new(
            new StubDataLoaderService().With(
                new SpellDefinition
                {
                    Id = MagicArrow, Key = "magic_arrow", Circle = 1, Mantra = "In Por Ylem", Action = 17,
                    Target = SpellTargetType.Mobile, Scroll = "arrowscroll"
                }
            ),
            Templates()
        );
    }

    private static ItemTemplateService Templates()
    {
        return new(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "arrowscroll", ItemId = new Serial(ArrowScroll) }
            )
        );
    }
}
