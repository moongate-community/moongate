using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Items;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class MountServiceTests
{
    private const int HorseMountGraphic = 0x3EA1;
    private static readonly Point3D Spot = new(1000, 1000, 0);

    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly StubNpcService _npcs = new();
    private readonly StubItemHandlingService _handling = new();
    private readonly CapturingLogSink _log = new();
    private readonly StubDeathService _death = new();
    private readonly RecordingDataAccess<ItemEntity> _itemData = new();
    private readonly ReservedInventory _inventory = new();
    private readonly MobileService _mobiles;
    private readonly Moongate.Server.Ultima.Services.ItemService _items;
    private readonly MountService _service;

    private readonly MobileEntity _rider = new()
    {
        Id = new(2), Name = "Aria", AccountId = new Serial(1002), Body = 400, Map = MapType.Felucca, Location = Spot
    };

    private readonly MobileEntity _horse = new()
    {
        Id = new(0x100), Name = "a horse", TemplateId = "horse", Body = 228, Map = MapType.Felucca,
        Location = new Point3D(1001, 1001, 0)
    };

    public MountServiceTests()
    {
        var sectors = TestSectors.Create();
        _mobiles = new(new StubMovementService(), sectors);
        _items = TestItems.Create(sectors);
        _handling.Templates["horse4"] = HorseMountGraphic;
        _handling.Templates["etherealhorse"] = 0x3EAA;
        _handling.Templates["ethereal_horse_statue"] = 0x20DD;
        _service = new(
            _mobiles,
            _items,
            _handling,
            _view,
            _npcs,
            new MobileTemplateService(
                new StubDataLoaderService().With(
                    new MobileTemplate
                    {
                        Id = "horse", Tags = new() { [MountProps.MountItemTag] = "horse4" }
                    },
                    new MobileTemplate
                    {
                        Id = "orc"
                    },
                    new MobileTemplate
                    {
                        Id = "ghosthorse", Tags = new() { [MountProps.MountItemTag] = "" }
                    }
                )
            ),
            _speech,
            new Lazy<IDeathService>(() => _death),
            _inventory,
            _itemData,
            new ItemTemplateService(
                new StubDataLoaderService().With(
                    new ItemTemplate
                    {
                        Id = "ethereal_horse_statue", ItemId = new Serial(0x20DD),
                        Tags = new() { [MountProps.MountItemTag] = "etherealhorse" }
                    },
                    new ItemTemplate { Id = "plain_statue", ItemId = new Serial(0x1224) }
                )
            ),
            new LoggerConfiguration().MinimumLevel.Information().WriteTo.Sink(_log).CreateLogger()
        )
        {
            RetryDelay = TimeSpan.Zero
        };
        _mobiles.EnterWorld(_rider);
        _mobiles.EnterWorld(_horse);
        _horse.SetProp(MountProps.Owner, (long)_rider.Id.Value);
    }

    [Fact]
    public void TryMount_OwnerNextToItsHorse_WearsTheMountItemAndRemovesThePet()
    {
        var mounted = _service.TryMount(_rider, _horse);

        Assert.True(mounted);
        Assert.True(_service.IsMounted(_rider));
        Assert.Equal(_horse.Id, Assert.Single(_npcs.Removals));
        var item = Assert.Single(_items.GetWorn(_rider.Id), worn => worn.Layer == LayerType.Mount);
        Assert.Equal(HorseMountGraphic, item.ItemId);
        Assert.False(item.Movable);
        Assert.Equal("horse", item.GetProp<string>(MountProps.PetTemplate));
        Assert.Equal((long)_rider.Id.Value, item.GetProp<long>(MountProps.PetOwner));
        Assert.Contains($"Worn {_rider.Id.Value} {item.Id.Value}", _view.Calls);
    }

    [Fact]
    public void TryMount_AnotherPlayersHorse_IsRefused()
    {
        _horse.SetProp(MountProps.Owner, 77L);

        Assert.False(_service.TryMount(_rider, _horse));

        Assert.False(_service.IsMounted(_rider));
        Assert.Empty(_npcs.Removals);
        Assert.Equal(501264, Assert.Single(_speech.ToldClilocs).Cliloc);
    }

    [Fact]
    public void TryMount_AWildHorse_IsRefused()
    {
        _horse.RemoveProp(MountProps.Owner);

        Assert.False(_service.TryMount(_rider, _horse));

        Assert.Equal(501263, Assert.Single(_speech.ToldClilocs).Cliloc);
    }

    [Fact]
    public void TryMount_AWildHorseByAGameMaster_IsRefusedToo()
    {
        _horse.RemoveProp(MountProps.Owner);

        Assert.False(_service.TryMount(_rider, _horse, force: true));

        Assert.Equal(501263, Assert.Single(_speech.ToldClilocs).Cliloc);
        Assert.Empty(_npcs.Removals);
    }

    [Fact]
    public void TryMount_AnotherPlayersHorseByAGameMaster_Mounts()
    {
        _horse.SetProp(MountProps.Owner, 77L);

        Assert.True(_service.TryMount(_rider, _horse, force: true));

        var item = Assert.Single(_items.GetWorn(_rider.Id), worn => worn.Layer == LayerType.Mount);
        Assert.Equal(77L, item.GetProp<long>(MountProps.PetOwner));
    }

    [Fact]
    public void TryMount_AHorseOnAnotherLevel_IsRefusedWithTooFar()
    {
        _horse.Location = new Point3D(1001, 1001, 20);

        Assert.False(_service.TryMount(_rider, _horse));

        Assert.Equal(500206, Assert.Single(_speech.ToldClilocs).Cliloc);
        Assert.Empty(_npcs.Removals);
    }

    [Fact]
    public void TryMount_ADyingCreature_IsRefusedInSilence()
    {
        _death.Dying.Add(_horse.Id);

        Assert.False(_service.TryMount(_rider, _horse));

        Assert.Empty(_speech.ToldClilocs);
        Assert.Empty(_npcs.Removals);
    }

    [Fact]
    public void TryMount_ARiderWhoseInventoryIsReserved_KeepsTheHorse()
    {
        _inventory.Reserved = true;

        Assert.False(_service.TryMount(_rider, _horse));

        Assert.False(_service.IsMounted(_rider));
        Assert.Empty(_npcs.Removals);
    }

    [Fact]
    public void TryMount_TwoTilesAway_IsRefusedWithTooFar()
    {
        _horse.Location = new Point3D(1002, 1000, 0);

        Assert.False(_service.TryMount(_rider, _horse));

        Assert.Equal(500206, Assert.Single(_speech.ToldClilocs).Cliloc);
        Assert.Empty(_npcs.Removals);
    }

    [Fact]
    public void TryMount_AlreadyMounted_IsRefused()
    {
        Assert.True(_service.TryMount(_rider, _horse));
        var second = new MobileEntity
        {
            Id = new(0x101), Name = "a horse", TemplateId = "horse", Map = MapType.Felucca, Location = Spot
        };
        second.SetProp(MountProps.Owner, (long)_rider.Id.Value);
        _mobiles.EnterWorld(second);

        Assert.False(_service.TryMount(_rider, second));

        Assert.Equal(1005583, Assert.Single(_speech.ToldClilocs).Cliloc);
        Assert.Single(_npcs.Removals);
    }

    [Fact]
    public void TryMount_ADeadRider_IsRefused()
    {
        _rider.Body = GhostBodies.GhostOf(400);

        Assert.False(_service.TryMount(_rider, _horse));

        Assert.False(_service.IsMounted(_rider));
        Assert.Empty(_npcs.Removals);
    }

    [Theory]
    [InlineData("orc")]
    [InlineData("ghosthorse")]
    [InlineData("unknown")]
    public void TryMount_ACreatureWithoutMountItem_IsRefusedInSilence(string template)
    {
        _horse.TemplateId = template;

        Assert.False(_service.TryMount(_rider, _horse));

        Assert.Empty(_speech.ToldClilocs);
        Assert.Empty(_npcs.Removals);
    }

    [Fact]
    public void TryMount_APlayer_IsRefused()
    {
        _horse.AccountId = new Serial(1003);

        Assert.False(_service.TryMount(_rider, _horse));
    }

    [Fact]
    public void TryMount_ThePetAlreadyRemoved_ReturnsFalseAndWearsNothing()
    {
        _npcs.Removes = false;

        Assert.False(_service.TryMount(_rider, _horse));

        Assert.False(_service.IsMounted(_rider));
    }

    [Fact]
    public void TryMount_TheMountItemDoesNotExist_ReturnsFalseAndKeepsThePet()
    {
        _handling.Templates.Clear();

        Assert.False(_service.TryMount(_rider, _horse));

        Assert.Empty(_npcs.Removals);
        Assert.Contains("horse4", Assert.Single(_log.Events).RenderMessage());
    }

    [Fact]
    public void IsMounted_AnItemOnTheMountLayerWorn_IsTrue()
    {
        var item = new ItemEntity { Id = new(0x40000900), TemplateId = "horse4", ItemId = HorseMountGraphic, Amount = 1 };
        _items.Add([item]);
        _items.Equip(item, _rider.Id, LayerType.Mount);

        Assert.True(_service.IsMounted(_rider));
    }

    [Fact]
    public void IsMounted_OnFoot_IsFalse()
    {
        Assert.False(_service.IsMounted(_rider));
    }

    [Fact]
    public async Task Dismount_Mounted_RemovesTheItemAndSpawnsTheHorseAtTheRiderWithItsOwner()
    {
        _service.TryMount(_rider, _horse);

        Assert.True(_service.Dismount(_rider));
        await _npcs.FirstSpawn.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(_service.IsMounted(_rider));
        Assert.Equal(("horse", MapType.Felucca, Spot), Assert.Single(_npcs.Spawns));
        Assert.Equal((long)_rider.Id.Value, _npcs.Spawned.GetProp<long>(MountProps.Owner));
        Assert.Contains(
            _view.Calls,
            call => call.StartsWith($"OwnItemRemoved {_rider.Id.Value} ", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Dismount_NotMounted_ReturnsFalse()
    {
        Assert.False(_service.Dismount(_rider));

        Assert.Empty(_npcs.Spawns);
    }

    [Fact]
    public void Dismount_RiderNotInTheWorld_SpawnsNothing()
    {
        _service.TryMount(_rider, _horse);
        _mobiles.LeaveWorld(_rider.Id);

        Assert.True(_service.Dismount(_rider));

        Assert.False(_service.IsMounted(_rider));
        Assert.Empty(_npcs.Spawns);
    }

    [Fact]
    public void Dismount_TheSpawnFails_TriesThreeTimesLogsEachAndTheItemIsGone()
    {
        _npcs.SpawnFailure = new InvalidOperationException("no room");
        _service.TryMount(_rider, _horse);

        Assert.True(_service.Dismount(_rider));
        SpinWait.SpinUntil(() => _log.Events.Count >= 3, TimeSpan.FromSeconds(5));

        Assert.False(_service.IsMounted(_rider));
        Assert.Equal(3, _npcs.Spawns.Count);
        Assert.All(_log.Events, line => Assert.Contains("could not be made again", line.RenderMessage()));
    }

    [Fact]
    public async Task Dismount_DeletesTheRowOfTheMountBeforeTheHorseComes()
    {
        _service.TryMount(_rider, _horse);
        _itemData.Upserted.Add(Assert.Single(_items.GetWorn(_rider.Id), worn => worn.Layer == LayerType.Mount));
        _npcs.Gate = new TaskCompletionSource();

        Assert.True(_service.Dismount(_rider));
        await _npcs.FirstSpawn.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(_itemData.Upserted);
        _npcs.Gate.SetResult();
    }

    [Fact]
    public void TryMountEthereal_AStatuetteInTheBackpack_IsDeleted_AndTheMountItemIsWorn()
    {
        var statuette = Statuette(inBackpack: true);

        var mounted = _service.TryMountEthereal(_rider, statuette);

        Assert.True(mounted);
        Assert.Same(statuette, Assert.Single(_handling.Deleted));
        var item = Assert.Single(_items.GetWorn(_rider.Id), worn => worn.Layer == LayerType.Mount);
        Assert.Equal((0x3EAA, false), (item.ItemId, item.Movable));
        Assert.Equal("ethereal_horse_statue", item.GetProp<string>(MountProps.EtherealTemplate));
        Assert.Empty(_npcs.Removals);
    }

    [Fact]
    public void TryMountEthereal_AStatuetteNotInTheBackpack_IsRefusedWithOnYourPerson()
    {
        var statuette = Statuette(inBackpack: false);

        Assert.False(_service.TryMountEthereal(_rider, statuette));

        Assert.Equal(1010095, Assert.Single(_speech.ToldClilocs).Cliloc);
        Assert.Empty(_handling.Deleted);
        Assert.False(_service.IsMounted(_rider));
    }

    [Fact]
    public void TryMountEthereal_AlreadyMounted_IsRefused_AndKeepsTheStatuette()
    {
        var statuette = Statuette(inBackpack: true);
        Assert.True(_service.TryMount(_rider, _horse));

        Assert.False(_service.TryMountEthereal(_rider, statuette));

        Assert.Equal(1005583, Assert.Single(_speech.ToldClilocs).Cliloc);
        Assert.Empty(_handling.Deleted);
    }

    [Fact]
    public void TryMountEthereal_ADeadRider_OrAnItemThatIsNoStatuette_IsRefusedInSilence()
    {
        var statuette = Statuette(inBackpack: true);
        var plain = new ItemEntity { Id = new(0x40000700), TemplateId = "plain_statue", ItemId = 0x1224, Amount = 1 };
        _rider.Body = GhostBodies.GhostOf(400);

        Assert.False(_service.TryMountEthereal(_rider, statuette));

        _rider.Body = 400;
        Assert.False(_service.TryMountEthereal(_rider, plain));
        Assert.Empty(_speech.ToldClilocs);
        Assert.Empty(_handling.Deleted);
    }

    [Fact]
    public void Dismount_OfAnEthereal_GivesTheStatuetteBack_AndSpawnsNoCreature()
    {
        _service.TryMountEthereal(_rider, Statuette(inBackpack: true));

        Assert.True(_service.Dismount(_rider));

        Assert.False(_service.IsMounted(_rider));
        Assert.Equal("ethereal_horse_statue", Assert.Single(_handling.Given).TemplateId);
        Assert.Empty(_npcs.Spawns);
    }

    [Fact]
    public void Dismount_OfAnEthereal_WithAFullBackpack_PutsTheStatueOnTheGroundAtTheRider()
    {
        _service.TryMountEthereal(_rider, Statuette(inBackpack: true));
        _handling.BackpackFull = true;

        Assert.True(_service.Dismount(_rider));

        Assert.Empty(_handling.Given);
        Assert.Contains(_view.Calls, call => call.StartsWith("Appeared ", StringComparison.Ordinal));
    }

    private ItemEntity Statuette(bool inBackpack)
    {
        var statuette = new ItemEntity
            { Id = new(0x40000600), TemplateId = "ethereal_horse_statue", ItemId = 0x20DD, Amount = 1 };

        if (inBackpack)
        {
            var backpack = new ItemEntity { Id = new(0x40000601), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
            backpack.Equip(_rider.Id, LayerType.Backpack);
            statuette.PutInContainer(backpack.Id, new Point2D(40, 40));
            _items.Add([backpack, statuette]);
        }
        else
        {
            statuette.PlaceOnGround(_rider.Map, _rider.Location);
            _items.Add([statuette]);
        }

        return statuette;
    }

    private sealed class ReservedInventory : IInventoryMutationGuard
    {
        public bool Reserved { get; set; }

        public bool Allows(ItemEntity item, Serial? destination = null)
        {
            return !Reserved;
        }

        public bool AllowsOwner(Serial mobileId)
        {
            return !Reserved;
        }
    }
}
