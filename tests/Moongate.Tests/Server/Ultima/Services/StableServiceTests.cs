using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Bank;
using Moongate.Server.Ultima.Types.Stable;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class StableServiceTests
{
    private static readonly Point3D Spot = new(1000, 1000, 0);

    private readonly StubNpcService _npcs = new();
    private readonly StubBankService _bank = new();
    private readonly StubDeathService _death = new();
    private readonly StubGameLoop _loop = new();
    private readonly Moongate.Tests.TestSupport.Ultima.Pets.StubPetService _pets = new();
    private readonly CapturingLogSink _log = new();
    private readonly StableConfig _config = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly StableService _service;

    private readonly MobileEntity _player = new()
    {
        Id = new(2), Name = "Aria", AccountId = new Serial(1002), Body = 400, Map = MapType.Felucca, Location = Spot
    };

    private readonly MobileEntity _horse = new()
    {
        Id = new(0x100), Name = "a horse", TemplateId = "horse", Body = 228, Map = MapType.Felucca,
        Location = new Point3D(1001, 1000, 0)
    };

    public StableServiceTests()
    {
        _service = new(
            _mobiles,
            _npcs,
            new MobileTemplateService(
                new StubDataLoaderService().With(
                    new MobileTemplate { Id = "horse", Tags = new() { [MountProps.MountItemTag] = "horse4" } },
                    new MobileTemplate { Id = "grayhorse", Tags = new() { [MountProps.MountItemTag] = "horse3" } },
                    new MobileTemplate { Id = "orc" }
                )
            ),
            _bank,
            _config,
            _loop,
            new Lazy<IDeathService>(() => _death),
            new LoggerConfiguration().MinimumLevel.Information().WriteTo.Sink(_log).CreateLogger(),
            new Lazy<IPetService>(() => _pets)
        )
        {
            RetryDelay = TimeSpan.Zero
        };
        _mobiles.EnterWorld(_player);
        _mobiles.EnterWorld(_horse);
        _horse.SetProp(MountProps.Owner, (long)_player.Id.Value);
        _bank.Carried[_player.Id] = 100;
    }

    [Fact]
    public void TryStable_OwnerPetNearby_RemovesItAndKeepsItsTemplate_AndTakesTheFee()
    {
        var result = _service.TryStable(_player, _horse);

        Assert.Equal(StableResultType.Ok, result);
        Assert.Equal(["horse"], _service.Stabled(_player));
        Assert.Equal(_horse.Id, Assert.Single(_npcs.Removals));
        Assert.Equal((30, true), (Assert.Single(_bank.Paid).Amount, Assert.Single(_bank.Paid).UseBank));
        Assert.Equal(70, _bank.Carried[_player.Id]);
    }

    [Fact]
    public void TryStable_AndTryClaim_TellThePetServiceTheFollowersChanged()
    {
        _service.TryStable(_player, _horse);
        Assert.Equal([_player.Id], _pets.ChangedFor);

        _service.TryClaim(_player, 0, "horse");

        Assert.Equal([_player.Id, _player.Id], _pets.ChangedFor);
    }

    [Fact]
    public void TryStable_ASecondPet_JoinsTheListInOrder()
    {
        var gray = new MobileEntity
        {
            Id = new(0x101), Name = "a horse", TemplateId = "grayhorse", Map = MapType.Felucca, Location = Spot
        };
        gray.SetProp(MountProps.Owner, (long)_player.Id.Value);
        _mobiles.EnterWorld(gray);

        _service.TryStable(_player, _horse);
        _service.TryStable(_player, gray);

        Assert.Equal(["horse", "grayhorse"], _service.Stabled(_player));
    }

    [Fact]
    public void TryStable_AFreeStable_TakesNoGold()
    {
        _config.Fee = 0;
        _bank.Carried.Clear();

        Assert.Equal(StableResultType.Ok, _service.TryStable(_player, _horse));

        Assert.Empty(_bank.Paid);
    }

    [Fact]
    public void TryStable_AWildOrAnotherOwnersPet_IsRefused()
    {
        _horse.SetProp(MountProps.Owner, 77L);

        Assert.Equal(StableResultType.NotYours, _service.TryStable(_player, _horse));

        _horse.RemoveProp(MountProps.Owner);

        Assert.Equal(StableResultType.NotYours, _service.TryStable(_player, _horse));
        Assert.Empty(_npcs.Removals);
        Assert.Empty(_bank.Paid);
    }

    [Fact]
    public void TryStable_ACreatureThatIsNoMount_OrAPlayer_IsNotAPet()
    {
        var orc = new MobileEntity
        {
            Id = new(0x102), Name = "an orc", TemplateId = "orc", Map = MapType.Felucca, Location = Spot
        };
        _mobiles.EnterWorld(orc);

        Assert.Equal(StableResultType.NotAPet, _service.TryStable(_player, orc));
        Assert.Equal(StableResultType.NotAPet, _service.TryStable(_player, _player));
    }

    [Fact]
    public void TryStable_APetThatIsNotInTheWorld_IsNotAPet()
    {
        _mobiles.LeaveWorld(_horse.Id);

        Assert.Equal(StableResultType.NotAPet, _service.TryStable(_player, _horse));
    }

    [Fact]
    public void TryStable_TwoTilesAwayOrOnAnotherLevel_IsTooFar()
    {
        _horse.Location = new Point3D(1002, 1000, 0);
        Assert.Equal(StableResultType.TooFar, _service.TryStable(_player, _horse));

        _horse.Location = new Point3D(1001, 1000, 20);
        Assert.Equal(StableResultType.TooFar, _service.TryStable(_player, _horse));
    }

    [Fact]
    public void TryStable_ADyingPet_IsRefused()
    {
        _death.Dying.Add(_horse.Id);

        Assert.Equal(StableResultType.Dying, _service.TryStable(_player, _horse));
        Assert.Empty(_npcs.Removals);
    }

    [Fact]
    public void TryStable_AFullStable_IsRefused()
    {
        _config.MaxPets = 1;
        _service.TryStable(_player, _horse);
        var gray = new MobileEntity
        {
            Id = new(0x101), Name = "a horse", TemplateId = "grayhorse", Map = MapType.Felucca, Location = Spot
        };
        gray.SetProp(MountProps.Owner, (long)_player.Id.Value);
        _mobiles.EnterWorld(gray);

        Assert.Equal(StableResultType.Full, _service.TryStable(_player, gray));
        Assert.Single(_npcs.Removals);
    }

    [Fact]
    public void TryStable_NotEnoughGold_KeepsThePet()
    {
        _bank.Carried[_player.Id] = 29;

        Assert.Equal(StableResultType.NoGold, _service.TryStable(_player, _horse));

        Assert.Empty(_npcs.Removals);
        Assert.Empty(_service.Stabled(_player));
        Assert.Empty(_bank.Paid);
    }

    [Fact]
    public void TryStable_TheFeeIsTakenFromTheBankWhenThePocketsAreShort()
    {
        _bank.Carried[_player.Id] = 10;
        _bank.Gold[_player.Id] = 50;

        Assert.Equal(StableResultType.Ok, _service.TryStable(_player, _horse));

        Assert.Equal(20, Assert.Single(_bank.Paid).FromBank);
    }

    [Fact]
    public void TryStable_TheCreatureCannotBeTaken_GivesTheFeeBack()
    {
        _npcs.Removes = false;

        Assert.Equal(StableResultType.Failed, _service.TryStable(_player, _horse));

        Assert.Empty(_service.Stabled(_player));
        Assert.Equal(30, Assert.Single(_bank.Given).Amount);
    }

    [Fact]
    public void TryStable_AnNpcAsPlayer_IsNoPlayer()
    {
        Assert.Equal(StableResultType.NoPlayer, _service.TryStable(_horse, _horse));
    }

    [Fact]
    public async Task TryClaim_SpawnsThePetAtThePlayerWithItsOwner_AndTakesItOffTheList()
    {
        _service.TryStable(_player, _horse);

        var result = _service.TryClaim(_player, 0, "horse");
        await _npcs.FirstSpawn.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(StableResultType.Ok, result);
        Assert.Empty(_service.Stabled(_player));
        Assert.Equal(("horse", MapType.Felucca, Spot), Assert.Single(_npcs.Spawns));
        Assert.Equal((long)_player.Id.Value, _npcs.Spawned.GetProp<long>(MountProps.Owner));
    }

    [Theory, InlineData(-1, "horse"), InlineData(1, "horse"), InlineData(0, "grayhorse")]
    public void TryClaim_APlaceOrATemplateThatIsNotThere_IsABadIndex(int index, string template)
    {
        _service.TryStable(_player, _horse);

        Assert.Equal(StableResultType.BadIndex, _service.TryClaim(_player, index, template));

        Assert.Equal(["horse"], _service.Stabled(_player));
        Assert.Empty(_npcs.Spawns);
    }

    [Fact]
    public void TryClaim_TwiceInARow_TheSecondFindsTheListShorter()
    {
        _service.TryStable(_player, _horse);

        Assert.Equal(StableResultType.Ok, _service.TryClaim(_player, 0, "horse"));
        Assert.Equal(StableResultType.BadIndex, _service.TryClaim(_player, 0, "horse"));
    }

    [Fact]
    public void TryClaim_ATemplateGoneFromTheData_IsDroppedAndLogged()
    {
        _player.SetProp(MountProps.Stabled, "vanished;horse");

        Assert.Equal(StableResultType.Failed, _service.TryClaim(_player, 0, "vanished"));

        Assert.Equal(["horse"], _service.Stabled(_player));
        Assert.Contains("vanished", Assert.Single(_log.Events).RenderMessage());
        Assert.Empty(_npcs.Spawns);
    }

    [Fact]
    public void TryClaim_APlayerNotInTheWorld_IsNoPlayer()
    {
        _service.TryStable(_player, _horse);
        _mobiles.LeaveWorld(_player.Id);

        Assert.Equal(StableResultType.NoPlayer, _service.TryClaim(_player, 0, "horse"));
        Assert.Equal(["horse"], _service.Stabled(_player));
    }

    [Fact]
    public void TryClaim_TheSpawnFails_TriesThreeTimesAndLogsEach()
    {
        _npcs.SpawnFailure = new InvalidOperationException("no room");
        _service.TryStable(_player, _horse);

        Assert.Equal(StableResultType.Ok, _service.TryClaim(_player, 0, "horse"));
        SpinWait.SpinUntil(() => _log.Events.Count >= 4, TimeSpan.FromSeconds(5));

        Assert.Equal(3, _npcs.Spawns.Count);
        Assert.Equal(3, _log.Events.Count(line => line.RenderMessage().Contains("could not be made again")));
    }

    [Fact]
    public void TryClaim_TheSpawnFailsThreeTimes_PutsThePetBackInTheStable()
    {
        _npcs.SpawnFailure = new InvalidOperationException("no room");
        _service.TryStable(_player, _horse);

        _service.TryClaim(_player, 0, "horse");
        SpinWait.SpinUntil(() => _service.Stabled(_player).Count == 1, TimeSpan.FromSeconds(5));

        Assert.Equal(["horse"], _service.Stabled(_player));
        Assert.Equal(3, _npcs.Spawns.Count);
    }

    [Fact]
    public void TryClaim_TheSpawnFailsAndThePlayerHasLeft_LogsThePetAsLost()
    {
        _npcs.SpawnFailure = new InvalidOperationException("no room");
        _service.TryStable(_player, _horse);
        _npcs.Gate = new TaskCompletionSource();

        _service.TryClaim(_player, 0, "horse");
        _mobiles.LeaveWorld(_player.Id);
        _npcs.Gate.SetResult();
        SpinWait.SpinUntil(() => _log.Events.Any(line => line.RenderMessage().Contains("is lost")), TimeSpan.FromSeconds(5));

        Assert.Contains(_log.Events, line => line.RenderMessage().Contains("is lost"));
        Assert.Empty(_service.Stabled(_player));
    }

    [Fact]
    public void TryClaim_NeverSpawnsOnTheGameLoopThread()
    {
        var loopThread = Environment.CurrentManagedThreadId;
        _npcs.OnLoopThread = () => Environment.CurrentManagedThreadId == loopThread;
        _service.TryStable(_player, _horse);

        _service.TryClaim(_player, 0, "horse");
        SpinWait.SpinUntil(() => _npcs.Spawns.Count == 1, TimeSpan.FromSeconds(5));

        Assert.Single(_npcs.Spawns);
        Assert.Empty(_log.Events);
    }

    [Fact]
    public void Stabled_SurvivesASnapshotOfThePlayer()
    {
        _service.TryStable(_player, _horse);

        var snapshot = _player.Snapshot();

        Assert.Equal("horse", snapshot.GetProp<string>(MountProps.Stabled));
    }
}
