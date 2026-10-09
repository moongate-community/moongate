using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Help;
using Moongate.Tests.TestSupport.Ultima.Help;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class HelpPageServiceTests : IAsyncLifetime
{
    private const long Staff = 7;
    private const long Player = 8;
    private const long OtherPlayer = 9;

    private BroadcastFixture _fixture = null!;
    private HelpPageServices _services = null!;

    private HelpPageService _service => _services.Service;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();

        foreach (var serial in new[] { Staff, Player, OtherPlayer })
        {
            var session = await _fixture.AddAsync(serial);

            if (serial == Staff)
            {
                await _fixture.Network.ExecuteOnLoopAsync(() => session.Set(
                        SessionKeys.AccountType,
                        AccountType.GameMaster
                    )
                );
            }
        }

        _services = HelpPageServices.Create(_fixture);
        await _service.StartAsync();
    }

    [Fact]
    public void Create_StoresThePageWithWhoAndWhere_AndTellsTheOnlineStaff()
    {
        var result = _service.Create(Mobile(Player), HelpPageKindType.Bug, "  The door is stuck  ");

        Assert.Equal(HelpPageCreateResultType.Ok, result.Type);
        var page = result.Page!;
        Assert.Equal(
            ("The door is stuck", HelpPageKindType.Bug, HelpPageStatusType.Open),
            (page.Text, page.Kind, page.Status)
        );
        Assert.Equal((new Serial((uint)Player), "Player", MapType.Trammel), (page.Player, page.PlayerName, page.Map));
        Assert.NotEqual(0, page.CreatedAt);
        Assert.Equal(1, _service.WaitingCount);
        Assert.Equal(["Player asks for help (Bug): The door is stuck"], Told(Staff));
        Assert.Empty(Told(Player));
    }

    [Fact]
    public void Create_TheTextIsCutAt128Characters()
    {
        var page = _service.Create(Mobile(Player), HelpPageKindType.Question, new string('a', 200)).Page!;

        Assert.Equal(128, page.Text.Length);
    }

    [Fact]
    public void Create_AnEmptyText_IsBadText_AndStoresNothing()
    {
        var result = _service.Create(Mobile(Player), HelpPageKindType.Question, "   ");

        Assert.Equal(HelpPageCreateResultType.BadText, result.Type);
        Assert.Equal(0, _service.WaitingCount);
        Assert.Empty(_service.Pages);
    }

    [Fact]
    public void Create_WhileThePlayerHasAnActivePage_IsAlreadyOpen()
    {
        _service.Create(Mobile(Player), HelpPageKindType.Question, "one");

        Assert.Equal(
            HelpPageCreateResultType.AlreadyOpen,
            _service.Create(Mobile(Player), HelpPageKindType.Question, "two").Type
        );
        Assert.Equal(1, _service.WaitingCount);
    }

    [Fact]
    public void Create_AfterTheLastWasClosed_WaitsForThePause_ThenIsAccepted()
    {
        var first = _service.Create(Mobile(Player), HelpPageKindType.Question, "one").Page!;
        _service.Close(first.Id, "Gino");

        var early = _service.Create(Mobile(Player), HelpPageKindType.Question, "two");
        Assert.Equal((HelpPageCreateResultType.Wait, 60), (early.Type, early.WaitSeconds));

        _services.Clock.Advance(TimeSpan.FromSeconds(61));

        Assert.Equal(HelpPageCreateResultType.Ok, _service.Create(Mobile(Player), HelpPageKindType.Question, "two").Type);
    }

    [Fact]
    public void Create_WithAZeroPause_NeverWaits()
    {
        _services.Config.PageCooldownSeconds = 0;
        var first = _service.Create(Mobile(Player), HelpPageKindType.Question, "one").Page!;
        _service.Close(first.Id, "Gino");

        Assert.Equal(HelpPageCreateResultType.Ok, _service.Create(Mobile(Player), HelpPageKindType.Question, "two").Type);
    }

    [Fact]
    public void Create_AnotherPlayer_IsNotBlockedByTheFirst()
    {
        _service.Create(Mobile(Player), HelpPageKindType.Question, "one");

        Assert.Equal(
            HelpPageCreateResultType.Ok,
            _service.Create(Mobile(OtherPlayer), HelpPageKindType.Question, "two").Type
        );
        Assert.Equal(2, _service.WaitingCount);
    }

    [Fact]
    public void CanCreate_AnswersWithoutCreating()
    {
        Assert.Equal(HelpPageCreateResultType.Ok, _service.CanCreate(new Serial((uint)Player)).Type);

        var page = _service.Create(Mobile(Player), HelpPageKindType.Question, "one").Page!;
        Assert.Equal(HelpPageCreateResultType.AlreadyOpen, _service.CanCreate(new Serial((uint)Player)).Type);

        _service.Close(page.Id, "Gino");
        var wait = _service.CanCreate(new Serial((uint)Player));

        Assert.Equal((HelpPageCreateResultType.Wait, 60), (wait.Type, wait.WaitSeconds));
        Assert.Single(_service.Pages);
    }

    [Fact]
    public void Take_MarksItTaken_ByName_AndATakenPageCanBeTakenOver()
    {
        var page = _service.Create(Mobile(Player), HelpPageKindType.Bug, "x").Page!;

        Assert.True(_service.Take(page.Id, "Gino"));
        Assert.Equal((HelpPageStatusType.Taken, "Gino"), (page.Status, page.TakenBy));
        Assert.True(_service.Take(page.Id, "Pina"));
        Assert.Equal("Pina", page.TakenBy);
        Assert.Equal(1, _service.WaitingCount);
    }

    [Fact]
    public void Take_ACloseOrUnknownPage_IsFalse()
    {
        var page = _service.Create(Mobile(Player), HelpPageKindType.Bug, "x").Page!;
        _service.Close(page.Id, "Gino");

        Assert.False(_service.Take(page.Id, "Pina"));
        Assert.False(_service.Take(new Serial(999), "Pina"));
        Assert.Equal("Gino", page.TakenBy);
    }

    [Fact]
    public void Answer_ToAnOnlinePlayer_TellsItAtOnce_AndMarksItDelivered()
    {
        var page = _service.Create(Mobile(Player), HelpPageKindType.Question, "x").Page!;

        Assert.True(_service.Answer(page.Id, "Gino", "Go north"));

        Assert.Equal(["Game master Gino answers: Go north"], Told(Player));
        Assert.Equal((HelpPageStatusType.Closed, true, "Go north"), (page.Status, page.AnswerDelivered, page.Answer));
        Assert.NotEqual(0, page.ClosedAt);
        Assert.Equal(0, _service.WaitingCount);
    }

    [Fact]
    public void Answer_ToAClosedPage_IsRefused_AndTheFirstAnswerStands()
    {
        var page = _service.Create(Mobile(Player), HelpPageKindType.Question, "x").Page!;
        _service.Answer(page.Id, "Gino", "first");

        Assert.False(_service.Answer(page.Id, "Pina", "second"));
        Assert.Equal("first", page.Answer);
        Assert.Single(Told(Player));
    }

    [Fact]
    public void Answer_AnEmptyAnswer_IsRefused_AndThePageStaysActive()
    {
        var page = _service.Create(Mobile(Player), HelpPageKindType.Question, "x").Page!;

        Assert.False(_service.Answer(page.Id, "Gino", "   "));
        Assert.True(page.IsActive);
    }

    [Fact]
    public void Answer_IsCutAt128Characters()
    {
        var page = _service.Create(Mobile(Player), HelpPageKindType.Question, "x").Page!;

        _service.Answer(page.Id, "Gino", new string('b', 300));

        Assert.Equal(128, page.Answer.Length);
    }

    [Fact]
    public async Task Answer_ToAnOfflinePlayer_WaitsForItsLogin_AndIsToldOnce()
    {
        var page = _service.Create(Mobile(Player), HelpPageKindType.Question, "x").Page!;
        _fixture.Mobiles.LeaveWorld(new Serial((uint)Player));

        _service.Answer(page.Id, "Gino", "Go north");
        Assert.False(page.AnswerDelivered);
        Assert.Empty(Told(Player));

        var back = EnterWorld(Player);
        await LoginAsync(back);
        await LoginAsync(back);

        Assert.Equal(["Game master Gino answers: Go north"], Told(Player));
        Assert.True(page.AnswerDelivered);
    }

    [Fact]
    public void Close_WithoutAnAnswer_DeliversNothing_AndATwiceCloseIsFalse()
    {
        var page = _service.Create(Mobile(Player), HelpPageKindType.Question, "x").Page!;

        Assert.True(_service.Close(page.Id, "Gino"));
        Assert.Empty(Told(Player));
        Assert.Equal((HelpPageStatusType.Closed, true), (page.Status, page.AnswerDelivered));
        Assert.False(_service.Close(page.Id, "Gino"));
    }

    [Fact]
    public async Task Login_OfStaff_TellsHowManyPagesWait_OnlyWhenThereAreSome()
    {
        await LoginAsync(Mobile(Staff));
        Assert.Empty(Told(Staff));

        _service.Create(Mobile(Player), HelpPageKindType.Question, "x");
        _services.Speech.Told.Clear();
        await LoginAsync(Mobile(Staff));

        Assert.Equal(["1 help requests are waiting. Type .pages."], Told(Staff));
    }

    [Fact]
    public async Task Login_OfAPlayer_DoesNotTellTheWaitingCount()
    {
        _service.Create(Mobile(OtherPlayer), HelpPageKindType.Question, "x");

        await LoginAsync(Mobile(Player));

        Assert.Empty(Told(Player));
    }

    [Fact]
    public void Active_ListsOpenAndTakenPages_OldestFirst()
    {
        var first = _service.Create(Mobile(Player), HelpPageKindType.Question, "one").Page!;
        _services.Clock.Advance(TimeSpan.FromSeconds(5));
        var second = _service.Create(Mobile(OtherPlayer), HelpPageKindType.Question, "two").Page!;
        _service.Take(first.Id, "Gino");

        Assert.Equal([first.Id, second.Id], _service.Active().Select(page => page.Id));

        _service.Close(first.Id, "Gino");

        Assert.Equal([second.Id], _service.Active().Select(page => page.Id));
    }

    [Fact]
    public async Task StartAsync_LoadsThePages_ContinuesTheNumbers_AndDropsTheOldClosedOnes()
    {
        var now = _services.Clock.GetUtcNow().ToUnixTimeMilliseconds();
        const long day = 86_400_000;
        var table = _services.Table;
        table.Upserted.Add(Row(5, Player, HelpPageStatusType.Open, now - 2 * day, 0));
        table.Upserted.Add(Row(6, OtherPlayer, HelpPageStatusType.Closed, now - 50 * day, now - 40 * day));
        table.Upserted.Add(Row(7, OtherPlayer, HelpPageStatusType.Closed, now - 2 * day, now - day));

        var restarted = _services.Build(_fixture);
        await restarted.StartAsync();

        Assert.Equal([5u, 7u], restarted.Pages.Select(page => page.Id.Value).Order());
        Assert.Equal([new Serial(6)], restarted.Capture());
        restarted.Committed([new Serial(6)]);
        Assert.Empty(restarted.Capture());

        var fresh = restarted.Create(Mobile(OtherPlayer), HelpPageKindType.Bug, "later");
        Assert.Equal(HelpPageCreateResultType.Ok, fresh.Type);
        Assert.Equal(8u, fresh.Page!.Id.Value);
    }

    [Fact]
    public async Task APageSurvivesARestart_AndTheAnswerOfAnOfflinePlayerIsStillDeliveredOnce()
    {
        var page = _service.Create(Mobile(Player), HelpPageKindType.Question, "x").Page!;
        _fixture.Mobiles.LeaveWorld(new Serial((uint)Player));
        _service.Answer(page.Id, "Gino", "Go north");
        _services.Table.Upserted.Add(page.Snapshot());
        await _service.StopAsync();

        var restarted = _services.Build(_fixture);
        await restarted.StartAsync();
        var back = EnterWorld(Player);
        await _services.Events.PublishAsync(new CharacterEnteredWorldEvent(back));
        await _services.Events.PublishAsync(new CharacterEnteredWorldEvent(back));

        Assert.Equal(["Game master Gino answers: Go north"], Told(Player));
        Assert.True(Assert.Single(restarted.Pages).AnswerDelivered);
        await restarted.StopAsync();
    }

    private static HelpPageEntity Row(uint id, long player, HelpPageStatusType status, long created, long closed)
    {
        return new()
        {
            Id = new Serial(id), Player = new Serial((uint)player), PlayerName = "Player", Kind = HelpPageKindType.Question,
            Text = "old", Status = status, CreatedAt = created, ClosedAt = closed,
            AnswerDelivered = status == HelpPageStatusType.Closed
        };
    }

    private MobileEntity Mobile(long serial)
    {
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)serial), out var mobile));

        return mobile;
    }

    private MobileEntity EnterWorld(long serial)
    {
        var mobile = new MobileEntity
        {
            Id = new Serial((uint)serial), Name = "Player", Map = MapType.Trammel, Location = new Point3D(10, 10, 0)
        };
        _fixture.Mobiles.EnterWorld(mobile);

        return mobile;
    }

    private List<string> Told(long serial)
    {
        return _services.Speech.Told.Where(told => told.Player.Id.Value == (uint)serial).Select(told => told.Text).ToList();
    }

    private async Task LoginAsync(MobileEntity character)
    {
        await _services.Events.PublishAsync(new CharacterEnteredWorldEvent(character));
    }

    public async Task DisposeAsync()
    {
        _services.Dispose();
        await _fixture.DisposeAsync();
    }
}
