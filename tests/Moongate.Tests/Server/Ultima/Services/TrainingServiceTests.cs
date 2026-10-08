using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Training;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class TrainingServiceTests : IAsyncLifetime
{
    private readonly ItemService _items = TestItems.Create();
    private readonly RecordingMobileStateService _state = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly StubItemSerialPool _serials = new();
    private readonly SkillsConfig _config = new();
    private uint _nextItem = 0x40001000;

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _player = null!;
    private MobileEntity _trainer = null!;
    private TrainingService _training = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var player));
        _player = player;
        _player.AccountId = new Serial(1002);
        _trainer = new MobileEntity { Id = new Serial(100), Name = "an alchemist", Map = MapType.Trammel };
        _trainer.Skills = [new() { Skill = SkillType.Alchemy, Base = 900 }, new() { Skill = SkillType.Tailoring, Base = 590 }];
        _player.Skills = [new() { Skill = SkillType.Alchemy, Base = 0 }, new() { Skill = SkillType.Tailoring, Base = 0 }];
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(new ItemTemplate { Id = "gold", ItemId = new Serial(0x0EED) })
        );
        var tiles = new FakeTileDataService().Item(0x0EED, TileFlagType.Generic, 0);
        _training = new(
            _fixture.Mobiles,
            _state,
            new ItemHandlingService(
                _items,
                _fixture.Sessions,
                _fixture.Sender,
                _view,
                TestTooltips.Create(_items, _fixture.Mobiles),
                new FakeItemFactoryService(templates, tiles),
                _serials
            ),
            _speech,
            _config,
            new ItemsConfig { GoldTemplate = "gold" }
        );
    }

    [Fact]
    public void Teachable_IsEverySkillTheTrainerHasAtSixtyOrMore_ThatThePlayerKnowsLessOf()
    {
        Assert.Equal([SkillType.Alchemy], _training.Teachable(_trainer, _player));

        _player.Skills[0].Base = 300;
        Assert.Empty(_training.Teachable(_trainer, _player));
    }

    [Fact]
    public void Teachable_AGhost_LearnsNothing()
    {
        _player.Body = 0x192;

        Assert.Empty(_training.Teachable(_trainer, _player));
    }

    [Fact]
    public void Quote_IsAThirdOfTheTrainersSkill_AtMostFortyTwo_ForOneGoldATenth()
    {
        Assert.True(Quote(SkillType.Alchemy));

        // 90.0 / 3 = 30.0: 300 tenths, 300 gold.
        var said = Assert.Single(_speech.SaidClilocs, spoken => spoken.Cliloc == 1019077);
        Assert.Equal(" 300", _speech.SaidAffixes[_speech.SaidClilocs.IndexOf(said)]);
        Assert.Contains(_speech.SaidClilocs, spoken => spoken.Cliloc == 1043108);
        Assert.Equal(new Serial(100), _session.Get(TrainingSessionKeys.Quote)!.Trainer);
    }

    [Fact]
    public void Quote_ATrainerOfOneHundred_TeachesNoMoreThanFortyTwo()
    {
        _trainer.Skills[0].Base = 1000;

        Quote(SkillType.Alchemy);

        Assert.Equal(" 333", _speech.SaidAffixes[_speech.SaidClilocs.FindIndex(spoken => spoken.Cliloc == 1019077)]);
    }

    [Fact]
    public void Quote_ATrainerBelowSixty_OrNotTeachingTheSkill_QuotesNothing()
    {
        Assert.False(Quote(SkillType.Tailoring));
        Assert.False(Quote(SkillType.Hiding));
        Assert.Empty(_speech.SaidClilocs);
    }

    [Fact]
    public void Quote_APlayerWhoKnowsMore_OrAsMuch_IsToldSo_AndNotQuoted()
    {
        _player.Skills[0].Base = 301;
        Assert.False(Quote(SkillType.Alchemy));
        _player.Skills[0].Base = 300;
        Assert.False(Quote(SkillType.Alchemy));

        Assert.Equal([501508, 501509], _speech.SaidClilocs.Select(spoken => spoken.Cliloc));
        Assert.Null(_session.Get(TrainingSessionKeys.Quote));
    }

    [Fact]
    public void Quote_ASkillNotLockedUp_OrWithNoRoomUnderTheTotalCap_IsRefusedWithTheClientsText()
    {
        _player.Skills[0].Lock = SkillLockType.Down;
        Assert.False(Quote(SkillType.Alchemy));

        _player.Skills[0].Lock = SkillLockType.Up;
        _player.Skills.Add(new MobileSkill { Skill = SkillType.Magery, Base = _config.TotalCap * 10, Lock = SkillLockType.Locked });
        Assert.False(Quote(SkillType.Alchemy));

        Assert.Equal([501510, 501510], _speech.ToldClilocs.Select(told => told.Cliloc));
        Assert.Equal([0x22, 0x22], _speech.ToldClilocHues);
    }

    [Fact]
    public void Quote_WithRoomOnlyInSkillsLockedDown_QuotesWhatTheyCouldGiveUp()
    {
        _player.Skills.Add(new MobileSkill { Skill = SkillType.Magery, Base = _config.TotalCap * 10 - 100, Lock = SkillLockType.Down });

        Assert.True(Quote(SkillType.Alchemy));

        // 100 tenths are free and the rest can come from Magery, which is locked down: the whole 300 is on offer.
        Assert.Equal(" 300", _speech.SaidAffixes[_speech.SaidClilocs.FindIndex(spoken => spoken.Cliloc == 1019077)]);
    }

    [Fact]
    public async Task Pay_ExactGold_RaisesTheSkillAtOnce_AndTakesTheGold()
    {
        Quote(SkillType.Alchemy);
        var gold = Gold(300);

        Assert.True(await OnLoopAsync(() => _training.Pay(_session, _trainer, gold)));

        Assert.Equal((_player, SkillType.Alchemy, 300, (int?)null), Assert.Single(_state.SkillsSet));
        Assert.False(_items.TryGet(gold.Id, out _));
        Assert.Contains(_speech.SaidClilocs, spoken => spoken.Cliloc == 501539);
        Assert.Equal(501540, Assert.Single(_speech.ToldClilocs).Cliloc);
        Assert.Null(_session.Get(TrainingSessionKeys.Quote));
    }

    [Fact]
    public async Task Pay_LessGold_TeachesLess()
    {
        Quote(SkillType.Alchemy);
        var gold = Gold(120);

        Assert.True(await OnLoopAsync(() => _training.Pay(_session, _trainer, gold)));

        Assert.Equal(120, Assert.Single(_state.SkillsSet).Value);
        Assert.False(_items.TryGet(gold.Id, out _));
    }

    [Fact]
    public async Task Pay_MoreGold_TakesOnlyThePrice_AndTheRestStaysWithThePlayer()
    {
        Quote(SkillType.Alchemy);
        var gold = Gold(1000);

        Assert.True(await OnLoopAsync(() => _training.Pay(_session, _trainer, gold)));

        Assert.Equal(300, Assert.Single(_state.SkillsSet).Value);
        Assert.Equal(700, gold.Amount);
    }

    [Fact]
    public async Task Pay_WithTheTotalCapFull_LowersTheSkillsLockedDown_InOrder()
    {
        _player.Skills.Add(new MobileSkill { Skill = SkillType.Magery, Base = _config.TotalCap * 10 - 100, Lock = SkillLockType.Down });
        _player.Skills.Add(new MobileSkill { Skill = SkillType.Hiding, Base = 100, Lock = SkillLockType.Up });
        Quote(SkillType.Alchemy);
        var gold = Gold(100);

        Assert.True(await OnLoopAsync(() => _training.Pay(_session, _trainer, gold)));

        // Hiding keeps its 100 and the total stays at the cap: Magery gives 100 up for the 100 learned.
        Assert.Equal(
            [(SkillType.Magery, _config.TotalCap * 10 - 200), (SkillType.Alchemy, 100)],
            _state.SkillsSet.Select(set => (set.Skill, set.Value))
        );
    }

    [Fact]
    public async Task Pay_WithoutAQuote_ForAnotherTrainer_OrNotGold_TakesNothing()
    {
        var gold = Gold(300);
        Assert.False(await OnLoopAsync(() => _training.Pay(_session, _trainer, gold)));

        Quote(SkillType.Alchemy);
        var other = new MobileEntity { Id = new Serial(101), Name = "another", Skills = _trainer.Skills };
        Assert.False(await OnLoopAsync(() => _training.Pay(_session, other, gold)));

        var sword = new ItemEntity { Id = new Serial(_nextItem++), TemplateId = "sword", ItemId = 0x0F5E, Amount = 300 };
        _items.Add([sword]);
        Assert.False(await OnLoopAsync(() => _training.Pay(_session, _trainer, sword)));

        Assert.Empty(_state.SkillsSet);
        Assert.Equal(300, gold.Amount);
    }

    [Fact]
    public async Task Pay_AfterTheStateChanged_WorksTheOfferOutAgain()
    {
        Quote(SkillType.Alchemy);
        _player.Skills[0].Base = 300;
        var gold = Gold(100);

        Assert.False(await OnLoopAsync(() => _training.Pay(_session, _trainer, gold)));

        Assert.Empty(_state.SkillsSet);
        Assert.Equal(100, gold.Amount);
        Assert.Null(_session.Get(TrainingSessionKeys.Quote));
    }

    [Fact]
    public async Task OnSessionClosed_ForgetsTheQuote()
    {
        Quote(SkillType.Alchemy);

        await OnLoopAsync(() =>
            {
                _training.OnSessionClosed(_session);

                return true;
            }
        );

        Assert.Null(_session.Get(TrainingSessionKeys.Quote));
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    // The quote keeps a value on the session, which only the game loop writes.
    private bool Quote(SkillType skill)
    {
        var result = false;
        _fixture.Network.ExecuteOnLoopAsync(() => result = _training.Quote(_session, _trainer, skill)).GetAwaiter().GetResult();

        return result;
    }

    private ItemEntity Gold(int amount)
    {
        var gold = new ItemEntity { Id = new Serial(_nextItem++), TemplateId = "gold", ItemId = 0x0EED, Amount = amount };
        _items.Add([gold]);

        return gold;
    }

    private async Task<bool> OnLoopAsync(Func<bool> action)
    {
        var result = false;
        await _fixture.Network.ExecuteOnLoopAsync(() => result = action());

        return result;
    }
}
