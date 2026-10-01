using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class BankServiceTests : IAsyncLifetime
{
    private readonly ItemService _items = TestItems.Create();
    private readonly FakeItemFactoryService _factory;
    private readonly SettableClock _time = new() { Now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero) };

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private BankService _bank = null!;

    public BankServiceTests()
    {
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = BankService.BankTemplate, ItemId = new Serial(0x0E7C), Name = "bank box", Movable = false }
            )
        );
        _factory = new(templates, new FakeTileDataService());
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var aria));
        _aria = aria;
        _aria.Location = new Point3D(1600, 1600, 0);
        var layouts = new ContainerLayoutService(
            new StubDataLoaderService().With(new ContainerContent { Name = "metal chest", Gump = 0x004A, Items = [0x0E7C], Default = true })
        );
        _bank = new(_items, _factory, _fixture.Sessions, _fixture.Mobiles, _fixture.Sender, TestTooltips.Create(_items, _fixture.Mobiles), layouts, _fixture.Network.Loop, time: _time);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task Open_TheFirstTime_CreatesTheBankBox_SavesIt_AndShowsIt()
    {
        Assert.True(await OnLoopAsync(() => _bank.Open(_aria)));
        await OnLoopAsync(() => true);

        var box = Assert.Single(_items.GetWorn(_aria.Id), item => item.Layer == LayerType.Bank);
        Assert.Equal(BankService.BankTemplate, box.TemplateId);
        Assert.Contains(_factory.Saved, saved => saved.Contains(box));
        Assert.Equal(box.Id, Assert.Single(_fixture.Sender.Sent.OfType<WornItemPacket>()).Item);
        Assert.Equal(0x004A, Assert.Single(_fixture.Sender.Sent.OfType<DisplayContainerPacket>()).Gump);
        Assert.Single(_fixture.Sender.Sent.OfType<ContainerContentPacket>());
        Assert.Contains(_fixture.Sender.Sent.OfType<UnicodeSpeechMessagePacket>(), message => message.Text == "Bank container has 0 items.");
        Assert.True(_bank.IsOpen(_aria));
    }

    [Fact]
    public async Task Open_ThePlayerLeavesBeforeTheBoxIsSaved_LeavesItToTheNextLogin()
    {
        await OnLoopAsync(
            () =>
            {
                _bank.Open(_aria);
                _fixture.Sessions.Remove(_session.SessionId);
                _fixture.Mobiles.LeaveWorld(_aria.Id);

                return true;
            }
        );
        await OnLoopAsync(() => true);

        Assert.Single(_factory.Saved);
        Assert.Empty(_items.GetWorn(_aria.Id));
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public async Task Open_ThePlayerLogsInAgainBeforeTheBoxIsSaved_ShowsNothingToTheOldCharacter()
    {
        var again = new MobileEntity { Id = _aria.Id, Name = "Aria", AccountId = _aria.AccountId, Map = MapType.Trammel, Location = _aria.Location };
        await OnLoopAsync(
            () =>
            {
                _bank.Open(_aria);
                _fixture.Mobiles.EnterWorld(again);

                return true;
            }
        );
        await OnLoopAsync(() => true);

        Assert.Empty(_items.GetWorn(_aria.Id));
        Assert.Empty(_fixture.Sender.Sent);
        Assert.False(_bank.IsOpen(again));
    }

    [Fact]
    public async Task Open_AnExistingBox_ShowsWhatIsInIt()
    {
        var (_, coin) = AddBank();

        Assert.True(await OnLoopAsync(() => _bank.Open(_aria)));

        Assert.Empty(_factory.Saved);
        Assert.Contains(coin.Id, Assert.Single(_fixture.Sender.Sent.OfType<ContainerContentPacket>()).Items.Select(item => item.Serial));
        Assert.Contains(_fixture.Sender.Sent.OfType<UnicodeSpeechMessagePacket>(), message => message.Text == "Bank container has 1 items.");
    }

    [Fact]
    public async Task IsOpen_EndsWhenThePlayerStepsChangesMapOrLogsInAgain()
    {
        AddBank();
        await OnLoopAsync(() => _bank.Open(_aria));

        _aria.Location = new Point3D(1601, 1600, 0);
        Assert.False(_bank.IsOpen(_aria));

        await OnLoopAsync(() => _bank.Open(_aria));
        _aria.Map = MapType.Felucca;
        Assert.False(_bank.IsOpen(_aria));

        _aria.Map = MapType.Trammel;
        Assert.True(_bank.IsOpen(_aria));
        Assert.False(_bank.IsOpen(new MobileEntity { Id = _aria.Id, Map = MapType.Trammel, Location = _aria.Location }));
    }

    [Fact]
    public async Task Close_EndsIt_EvenBackOnTheSameSpot()
    {
        AddBank();
        await OnLoopAsync(() => _bank.Open(_aria));

        _bank.Close(_aria);

        Assert.False(_bank.IsOpen(_aria));
    }

    [Fact]
    public async Task ASessionClosing_EndsIt()
    {
        AddBank();
        await OnLoopAsync(() => _bank.Open(_aria));

        _bank.OnSessionClosed(_session);

        Assert.False(_bank.IsOpen(_aria));
    }

    [Fact]
    public async Task Open_AgainWithinASecond_ShowsTheBankOnce_AsManyBankersHearTheSameWord()
    {
        AddBank();

        await OnLoopAsync(() => _bank.Open(_aria) && _bank.Open(_aria));
        _time.Advance(TimeSpan.FromSeconds(1));
        await OnLoopAsync(() => _bank.Open(_aria));

        Assert.Equal(2, _fixture.Sender.Sent.OfType<DisplayContainerPacket>().Count());
    }

    [Fact]
    public async Task Open_APlayerWithoutASession_IsRefused()
    {
        var ghost = new MobileEntity { Id = new Serial(9), Map = MapType.Trammel };

        Assert.False(await OnLoopAsync(() => _bank.Open(ghost)));
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public async Task CanAccess_TheBankOnlyWhileItIsOpen_ExceptStaff()
    {
        var (_, coin) = AddBank();
        var dagger = new ItemEntity { Id = new Serial(0x40000020), TemplateId = "dagger", ItemId = 0x0F51, Amount = 1 };
        dagger.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        _items.Add([dagger]);

        Assert.False(_bank.CanAccess(_session, _aria, coin));
        Assert.True(_bank.CanAccess(_session, _aria, dagger));

        await OnLoopAsync(() => _bank.Open(_aria));
        Assert.True(_bank.CanAccess(_session, _aria, coin));

        _aria.Location = new Point3D(1602, 1600, 0);
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));
        Assert.True(_bank.CanAccess(_session, _aria, coin));
    }

    [Fact]
    public async Task CanAccess_SomeoneElsesBank_IsRefusedEvenOpen()
    {
        var (_, coin) = AddBank();
        var bob = await _fixture.AddAsync(3);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(3), out var bobby));

        await OnLoopAsync(() => _bank.Open(_aria));

        Assert.False(_bank.CanAccess(bob, bobby, coin));
    }

    private (ItemEntity Box, ItemEntity Coin) AddBank()
    {
        var box = new ItemEntity { Id = new Serial(0x40000010), TemplateId = BankService.BankTemplate, ItemId = 0x0E7C, Amount = 1 };
        box.Equip(_aria.Id, LayerType.Bank);
        var coin = new ItemEntity { Id = new Serial(0x40000011), TemplateId = "gold", ItemId = 0x0EED, Amount = 5 };
        coin.PutInContainer(box.Id, new Point2D(44, 65));
        _items.Add([box, coin]);

        return (box, coin);
    }

    private async Task<bool> OnLoopAsync(Func<bool> action)
    {
        var result = false;
        await _fixture.Network.ExecuteOnLoopAsync(() => result = action());

        return result;
    }
}
