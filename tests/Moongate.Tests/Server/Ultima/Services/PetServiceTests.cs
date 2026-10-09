using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Data.Taming;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Pets;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class PetServiceTests
{
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly ItemService _items = TestItems.Create();
    private readonly SettableClock _time = new();
    private readonly PetsConfig _config = new();
    private readonly PetService _service;

    private readonly MobileEntity _player = new()
    {
        Id = new(2), Name = "Aria", AccountId = new Serial(1002), Body = 400, Map = MapType.Felucca,
        Location = new Point3D(1000, 1000, 0)
    };

    public PetServiceTests()
    {
        _service = new(
            _mobiles,
            _items,
            new TamingService(
                new StubDataLoaderService().With(
                    new TamingCreature { Template = "horse", MinSkill = 29.1, Slots = 1 },
                    new TamingCreature { Template = "drake", MinSkill = 90, Slots = 3 }
                )
            ),
            _config,
            _time
        );
        _mobiles.EnterWorld(_player);
    }

    [Fact]
    public void TryTame_AWildTamableCreature_BecomesThePlayersOwn()
    {
        var horse = Creature(0x100, "horse");

        Assert.Equal(PetResultType.Ok, _service.TryTame(_player, horse));

        Assert.Equal((long)_player.Id.Value, horse.GetProp<long>(MountProps.Owner));
        Assert.Equal(1, _service.Followers(_player));
    }

    [Fact]
    public void Release_ACreatureOfThePlayer_LosesItsOwnerAndItsOrder_AndIsOneFollowerLess()
    {
        var horse = Creature(0x100, "horse");
        _service.TryTame(_player, horse);
        horse.SetProp(MountProps.PetOrder, "stay");
        Assert.Equal(1, _service.Followers(_player));

        Assert.True(_service.Release(_player, horse));

        Assert.False(horse.TryGetProp<long>(MountProps.Owner, out _));
        Assert.False(horse.TryGetProp<string>(MountProps.PetOrder, out _));
        Assert.Equal(0, _service.Followers(_player));
    }

    [Fact]
    public void Release_SomeoneElsesCreatureAWildOneAPlayerOrOneNotInTheWorld_IsRefused()
    {
        var horse = Creature(0x100, "horse");
        var gone = new MobileEntity { Id = new(0x101), TemplateId = "horse", Map = MapType.Felucca };
        gone.SetProp(MountProps.Owner, (long)_player.Id.Value);

        Assert.False(_service.Release(_player, horse));
        horse.SetProp(MountProps.Owner, 77L);
        Assert.False(_service.Release(_player, horse));
        Assert.False(_service.Release(_player, _player));
        Assert.False(_service.Release(_player, gone));
        Assert.False(_service.Release(horse, horse));
        Assert.Equal(77L, horse.GetProp<long>(MountProps.Owner));
    }

    [Fact]
    public void TryTame_ACreatureWithNoEntry_IsNotTamable()
    {
        Assert.Equal(PetResultType.NotTamable, _service.TryTame(_player, Creature(0x100, "orc")));
    }

    [Fact]
    public void TryTame_ACreatureWithAnOwner_IsAlreadyOwned()
    {
        var horse = Creature(0x100, "horse");
        horse.SetProp(MountProps.Owner, 77L);

        Assert.Equal(PetResultType.AlreadyOwned, _service.TryTame(_player, horse));
    }

    [Fact]
    public void TryTame_APlayerOrAnItemOrACreatureNotInTheWorld_IsNotAnNpc()
    {
        var gone = new MobileEntity { Id = new(0x101), TemplateId = "horse", Map = MapType.Felucca };

        Assert.Equal(PetResultType.NotAnNpc, _service.TryTame(_player, _player));
        Assert.Equal(PetResultType.NotAnNpc, _service.TryTame(_player, gone));
    }

    [Fact]
    public void TryTame_AnNpcAsTamer_IsNoPlayer()
    {
        var horse = Creature(0x100, "horse");

        Assert.Equal(PetResultType.NoPlayer, _service.TryTame(horse, horse));
    }

    [Fact]
    public void TryTame_TheSlotsDoNotFit_IsTooManyFollowers_EvenRightAfterAnotherTame()
    {
        for (var index = 0; index < 5; index++)
        {
            Assert.Equal(PetResultType.Ok, _service.TryTame(_player, Creature((uint)(0x100 + index), "horse")));
        }

        var sixth = Creature(0x200, "horse");

        Assert.Equal(PetResultType.TooManyFollowers, _service.TryTame(_player, sixth));
        Assert.False(sixth.TryGetProp<long>(MountProps.Owner, out _));
    }

    [Fact]
    public void TryTame_ABigCreature_CountsForItsSlots()
    {
        Assert.Equal(PetResultType.Ok, _service.TryTame(_player, Creature(0x100, "drake")));
        Assert.Equal(3, _service.Followers(_player));
        Assert.Equal(PetResultType.Ok, _service.TryTame(_player, Creature(0x101, "horse")));
        Assert.Equal(PetResultType.Ok, _service.TryTame(_player, Creature(0x102, "horse")));
        Assert.Equal(PetResultType.TooManyFollowers, _service.TryTame(_player, Creature(0x103, "drake")));
    }

    [Fact]
    public void Followers_CountsTheCreatureTheRiderRides_ButNotTheOnesOfAnotherPlayer()
    {
        var other = Creature(0x100, "horse");
        other.SetProp(MountProps.Owner, 77L);
        var mount = new ItemEntity { Id = new(0x40000500), TemplateId = "horse4", ItemId = 0x3EA1, Amount = 1 };
        mount.SetProp(MountProps.PetTemplate, "drake");
        mount.SetProp(MountProps.PetOwner, (long)_player.Id.Value);
        _items.Add([mount]);
        _items.Equip(mount, _player.Id, LayerType.Mount);

        Assert.Equal(3, _service.Followers(_player));
    }

    [Fact]
    public void Followers_ACreatureOfThePlayerRiddenByAGameMaster_CountsForTheOwner_NotForTheRider()
    {
        var master = new MobileEntity
        {
            Id = new(3), Name = "Giachi", AccountId = new Serial(1003), Body = 400, Map = MapType.Felucca,
            Location = new Point3D(1001, 1000, 0)
        };
        _mobiles.EnterWorld(master);
        var mount = new ItemEntity { Id = new(0x40000500), TemplateId = "horse4", ItemId = 0x3EA1, Amount = 1 };
        mount.SetProp(MountProps.PetTemplate, "drake");
        mount.SetProp(MountProps.PetOwner, (long)_player.Id.Value);
        _items.Add([mount]);
        _items.Equip(mount, master.Id, LayerType.Mount);

        Assert.Equal(3, _service.Followers(_player));
        Assert.Equal(0, _service.Followers(master));
    }

    [Fact]
    public void TryTame_TakesTheCreatureOutOfItsSpawnRegion()
    {
        var horse = Creature(0x100, "horse");
        horse.SetProp(SpawnRegionService.RegionProp, "britain");

        _service.TryTame(_player, horse);

        Assert.False(horse.TryGetProp<string>(SpawnRegionService.RegionProp, out _));
    }

    [Fact]
    public void Changed_MakesTheNextCountFresh_WithoutWaitingForTheMemory()
    {
        var horse = Creature(0x100, "horse");
        horse.SetProp(MountProps.Owner, (long)_player.Id.Value);
        Assert.Equal(1, _service.Followers(_player));
        _mobiles.LeaveWorld(horse.Id);

        _service.Changed(_player.Id);

        Assert.Equal(0, _service.Followers(_player));
    }

    [Fact]
    public void Followers_ACreatureWithNoEntry_CountsForOne()
    {
        var orc = Creature(0x100, "orc");
        orc.SetProp(MountProps.Owner, (long)_player.Id.Value);

        Assert.Equal(1, _service.Followers(_player));
        Assert.Equal(1, _service.SlotsOf(null));
        Assert.Equal(3, _service.SlotsOf("drake"));
    }

    [Fact]
    public void Followers_AreRememberedForAMoment_ThenCountedAgain()
    {
        var horse = Creature(0x100, "horse");
        horse.SetProp(MountProps.Owner, (long)_player.Id.Value);
        Assert.Equal(1, _service.Followers(_player));

        _mobiles.LeaveWorld(horse.Id);

        Assert.Equal(1, _service.Followers(_player));

        _time.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(0, _service.Followers(_player));
    }

    private MobileEntity Creature(uint serial, string template)
    {
        var creature = new MobileEntity
        {
            Id = new(serial), Name = template, TemplateId = template, Map = MapType.Felucca,
            Location = new Point3D(1001, 1000, 0)
        };
        _mobiles.EnterWorld(creature);

        return creature;
    }
}
