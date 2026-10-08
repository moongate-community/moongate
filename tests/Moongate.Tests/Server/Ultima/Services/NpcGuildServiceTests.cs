using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Guilds;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class NpcGuildServiceTests : IAsyncLifetime
{
    private readonly ItemService _items = TestItems.Create();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly StubItemSerialPool _serials = new();
    private readonly SettableClock _clock = new();
    private uint _nextItem = 0x40001000;

    private BroadcastFixture _fixture = null!;
    private MobileEntity _player = null!;
    private MobileEntity _smith = null!;
    private MobileEntity _thief = null!;
    private NpcGuildService _guilds = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        var session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var player));
        _player = player;
        _player.AccountId = new Serial(1002);
        _smith = new MobileEntity { Id = new Serial(100), TemplateId = "smith", Name = "Bob" };
        _thief = new MobileEntity { Id = new Serial(101), TemplateId = "thief", Name = "Zed" };
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(new ItemTemplate { Id = "gold", ItemId = new Serial(0x0EED) })
        );
        var tiles = new FakeTileDataService().Item(0x0EED, TileFlagType.Generic, 0);
        _guilds = new(
            new MobileTemplateService(
                new StubDataLoaderService().With(
                    new MobileTemplate { Id = "smith", NpcGuild = NpcGuildType.Blacksmiths },
                    new MobileTemplate { Id = "thief", NpcGuild = NpcGuildType.Thieves },
                    new MobileTemplate { Id = "orc" }
                )
            ),
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
            new ItemsConfig { GoldTemplate = "gold" },
            _clock
        );
        _ = session;
    }

    [Fact]
    public void Of_IsTheGuildOfTheTemplate_AndNothingForOtherNpcs()
    {
        Assert.Equal(NpcGuildType.Blacksmiths, _guilds.Of(_smith));
        Assert.Null(_guilds.Of(new MobileEntity { Id = new Serial(102), TemplateId = "orc" }));
        Assert.Null(_guilds.Of(_player));
    }

    [Fact]
    public void Quote_TellsThePrice_ToAPlayerInNoGuild()
    {
        Assert.True(_guilds.Quote(_smith, _player));

        Assert.Equal((1008052, " 500"), (Assert.Single(_speech.SaidClilocs).Cliloc, Assert.Single(_speech.SaidAffixes)));
    }

    [Fact]
    public async Task Join_ExactGold_MakesThePlayerAMember_AndTakesTheGold()
    {
        var gold = Gold(500);

        Assert.True(await OnLoopAsync(() => _guilds.Join(_smith, _player, gold)));

        Assert.Equal(NpcGuildType.Blacksmiths, _guilds.MemberOf(_player));
        Assert.False(_items.TryGet(gold.Id, out _));
        Assert.Equal(1008054, Assert.Single(_speech.SaidClilocs).Cliloc);
    }

    [Theory,
     InlineData(499),
     InlineData(501)]
    public async Task Join_AnyOtherAmountOfGold_DoesNothing(int amount)
    {
        var gold = Gold(amount);

        Assert.False(await OnLoopAsync(() => _guilds.Join(_smith, _player, gold)));

        Assert.Null(_guilds.MemberOf(_player));
        Assert.Equal(amount, gold.Amount);
    }

    [Fact]
    public async Task Join_AMemberOfThatGuild_OrOfAnother_IsToldSo()
    {
        Assert.True(await OnLoopAsync(() => _guilds.Join(_smith, _player, Gold(500))));
        _speech.SaidClilocs.Clear();

        Assert.False(await OnLoopAsync(() => _guilds.Join(_smith, _player, Gold(500))));
        Assert.False(_guilds.Quote(_smith, _player));
        Assert.False(_guilds.Quote(_thief, _player));

        Assert.Equal([501047, 501047, 501046], _speech.SaidClilocs.Select(said => said.Cliloc));
    }

    [Fact]
    public async Task Join_TheThieves_WantNoKills_AndStealingSixty()
    {
        _player.Kills = 1;
        Assert.False(await OnLoopAsync(() => _guilds.Join(_thief, _player, Gold(500))));

        _player.Kills = 0;
        Assert.False(await OnLoopAsync(() => _guilds.Join(_thief, _player, Gold(500))));

        _player.Skills = [new MobileSkill { Skill = SkillType.Stealing, Base = 600 }];
        Assert.True(await OnLoopAsync(() => _guilds.Join(_thief, _player, Gold(500))));

        Assert.Equal([501050, 501051, 1008054], _speech.SaidClilocs.Select(said => said.Cliloc));
        Assert.Equal(NpcGuildType.Thieves, _guilds.MemberOf(_player));
    }

    [Fact]
    public async Task Resign_NeedsAWeek_ThenLeavesTheGuild()
    {
        await OnLoopAsync(() => _guilds.Join(_smith, _player, Gold(500)));
        _speech.SaidClilocs.Clear();

        Assert.False(_guilds.Resign(_smith, _player));
        _clock.Advance(TimeSpan.FromDays(7).Add(TimeSpan.FromMinutes(1)));
        Assert.True(_guilds.Resign(_smith, _player));

        Assert.Equal([501053, 501054], _speech.SaidClilocs.Select(said => said.Cliloc));
        Assert.Null(_guilds.MemberOf(_player));
    }

    [Fact]
    public void Resign_ByAPlayerOfAnotherGuild_OrOfNone_IsToldHeIsNoMember()
    {
        Assert.False(_guilds.Resign(_smith, _player));

        Assert.Equal(501052, Assert.Single(_speech.SaidClilocs).Cliloc);
    }

    [Fact]
    public async Task AGhost_OrANpc_CannotJoin()
    {
        _player.Body = 0x192;

        Assert.False(await OnLoopAsync(() => _guilds.Join(_smith, _player, Gold(500))));
        Assert.False(_guilds.Quote(_smith, _player));
        Assert.False(_guilds.Quote(_smith, _thief));
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
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
