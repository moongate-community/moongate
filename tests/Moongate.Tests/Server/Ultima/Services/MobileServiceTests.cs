using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Npcs;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class MobileServiceTests
{
    [Fact]
    public void HairAndBeardSerials_AreVirtualDistinctAndStablePerMobile()
    {
        var mobiles = new MobileService(new StubMovementService(), TestSectors.Create());
        var aria = new Serial(0x00000002);

        var hair = mobiles.HairSerial(aria);
        var beard = mobiles.BeardSerial(aria);

        Assert.True(hair.IsVirtual);
        Assert.True(beard.IsVirtual);
        Assert.NotEqual(hair, beard);
        Assert.Equal(hair, mobiles.HairSerial(aria));
        Assert.Equal(beard, mobiles.BeardSerial(aria));
    }

    [Fact]
    public void HairSerials_OfDifferentMobiles_Differ()
    {
        var mobiles = new MobileService(new StubMovementService(), TestSectors.Create());

        Assert.NotEqual(mobiles.HairSerial(new Serial(2)), mobiles.HairSerial(new Serial(3)));
    }

    [Fact]
    public void FirstVirtualSerial_IsTheStartOfTheVirtualRange()
    {
        Assert.Equal(new Serial(Serial.MinVirtual), new MobileService(new StubMovementService(), TestSectors.Create()).HairSerial(new Serial(2)));
    }

    [Fact]
    public void EnterWorld_KeepsTheLiveMobile()
    {
        var mobiles = new MobileService(new StubMovementService(), TestSectors.Create());
        var aria = Aria();

        mobiles.EnterWorld(aria);

        Assert.True(mobiles.IsInWorld(aria.Id));
        Assert.False(mobiles.IsInWorld(new Serial(3)));
        Assert.Equal([aria.Id], mobiles.InWorld);
        Assert.True(mobiles.TryGet(aria.Id, out var live));
        Assert.Same(aria, live);
        Assert.Equal([aria], mobiles.Mobiles);
    }

    [Fact]
    public void LeaveWorld_ForgetsTheMobile()
    {
        var mobiles = new MobileService(new StubMovementService(), TestSectors.Create());
        var aria = Aria();
        mobiles.EnterWorld(aria);

        Assert.True(mobiles.LeaveWorld(aria.Id));

        Assert.False(mobiles.IsInWorld(aria.Id));
        Assert.False(mobiles.TryGet(aria.Id, out _));
        Assert.False(mobiles.LeaveWorld(aria.Id));
    }

    [Fact]
    public void EnterWorld_TellsTheSensesAfterTheSectors()
    {
        var senses = new RecordingNpcSenseService();
        var sectors = TestSectors.Create();
        var mobiles = new MobileService(new StubMovementService(), sectors, senses);
        var aria = new MobileEntity { Id = new Serial(2), Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0) };

        mobiles.EnterWorld(aria);

        Assert.Equal(["Appeared 2"], senses.Calls);
        Assert.Contains(aria, sectors.GetMobilesInRange(MapType.Trammel, aria.Location, 0));
    }

    [Fact]
    public void TryMove_AStep_TellsTheSensesWhereItCameFrom_ATurnDoesNot()
    {
        var senses = new RecordingNpcSenseService();
        var mobiles = new MobileService(new StubMovementService(), TestSectors.Create(), senses);
        var aria = new MobileEntity
        {
            Id = new Serial(2), Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0), Direction = DirectionType.North
        };
        mobiles.EnterWorld(aria);
        senses.Calls.Clear();

        mobiles.TryMove(aria, DirectionType.East);
        mobiles.TryMove(aria, DirectionType.East);

        Assert.Equal(["Moved 2 1600,1600,0"], senses.Calls);
    }

    [Fact]
    public void EnterMoveAndLeave_KeepThePlayersRegion()
    {
        var regions = new RegionService(
            new StubDataLoaderService().With(
                new RegionContent
                {
                    Map = MapType.Trammel, Name = "Britain", Areas = [new RegionAreaContent { X1 = 1500, Y1 = 1500, X2 = 1601, Y2 = 1700 }]
                }
            )
        );
        var mobiles = new MobileService(new StubMovementService(), TestSectors.Create(), regions: regions);
        var aria = new MobileEntity
        {
            Id = new Serial(2), AccountId = new Serial(0x42), Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0),
            Direction = DirectionType.East
        };

        mobiles.EnterWorld(aria);
        Assert.Equal("Britain", regions.Current(aria.Id)?.Name);

        mobiles.TryMove(aria, DirectionType.East);
        Assert.Null(regions.Current(aria.Id));

        // The first step west only turns the player.
        mobiles.TryMove(aria, DirectionType.West);
        mobiles.TryMove(aria, DirectionType.West);
        Assert.Equal("Britain", regions.Current(aria.Id)?.Name);
        mobiles.LeaveWorld(aria.Id);

        Assert.Null(regions.Current(aria.Id));
    }

    [Fact]
    public void TryMove_ADifferentDirection_OnlyTurns()
    {
        var movement = new StubMovementService();
        var aria = Aria();
        aria.Direction = DirectionType.South;

        var result = new MobileService(movement, TestSectors.Create()).TryMove(aria, DirectionType.East);

        Assert.Equal(MoveResultType.Turned, result);
        Assert.Equal(DirectionType.East, aria.Direction);
        Assert.Equal(new Point3D(1496, 1628, 10), aria.Location);
        Assert.Empty(movement.Checks);
    }

    [Fact]
    public void TryMove_TheFacedDirection_StepsToTheNextCellAtTheLandingHeight()
    {
        var movement = new StubMovementService { LandingZ = 12 };
        var aria = Aria();
        aria.Direction = DirectionType.East;

        var result = new MobileService(movement, TestSectors.Create()).TryMove(aria, DirectionType.East);

        Assert.Equal(MoveResultType.Moved, result);
        Assert.Equal(new Point3D(1497, 1628, 12), aria.Location);
        Assert.Equal((MapType.Trammel, new Point3D(1496, 1628, 10), DirectionType.East), Assert.Single(movement.Checks));
    }

    [Fact]
    public void TryMove_TheRunningBit_IsNotPartOfTheDirection()
    {
        var aria = Aria();
        aria.Direction = DirectionType.East;

        var result = new MobileService(new StubMovementService(), TestSectors.Create()).TryMove(aria, DirectionType.East | DirectionType.Running);

        Assert.Equal(MoveResultType.Moved, result);
        Assert.Equal(DirectionType.East, aria.Direction);
        Assert.Equal(new Point3D(1497, 1628, 0), aria.Location);
    }

    [Fact]
    public void TryMove_ABlockedStep_LeavesTheMobileWhereItIs()
    {
        var aria = Aria();
        aria.Direction = DirectionType.East;

        var result = new MobileService(new StubMovementService { Allow = false }, TestSectors.Create()).TryMove(aria, DirectionType.East);

        Assert.Equal(MoveResultType.Blocked, result);
        Assert.Equal(new Point3D(1496, 1628, 10), aria.Location);
    }

    [Fact]
    public void TryMove_OnAMapThatIsNotLoaded_IsBlocked()
    {
        var aria = Aria();
        aria.Direction = DirectionType.East;

        var result = new MobileService(new StubMovementService { ThrowMapNotLoaded = true }, TestSectors.Create()).TryMove(aria, DirectionType.East);

        Assert.Equal(MoveResultType.Blocked, result);
        Assert.Equal(new Point3D(1496, 1628, 10), aria.Location);
    }

    [Fact]
    public void GetStatus_MapsTheMobileWithTheStatCapAndFollowersMax()
    {
        var status = new MobileService(new StubMovementService(), TestSectors.Create()).GetStatus(Aria());

        Assert.Equal(
            (new Serial(2), "Aria", 60, 61, true, 62, 20, 10, 21, 22, 11, 12, RaceType.Elf),
            (status.Serial, status.Name, status.Hits, status.HitsMax, status.Female, status.Strength, status.Dexterity,
                status.Intelligence, status.Stamina, status.StaminaMax, status.Mana, status.ManaMax, status.Race)
        );
        Assert.Equal((1, 2, 3, 4, 5), (status.PhysicalResistance, status.FireResistance, status.ColdResistance,
            status.PoisonResistance, status.EnergyResistance));
        Assert.Equal((225, 5), (status.StatCap, status.FollowersMax));
    }

    [Fact]
    public void GetEquipment_ListsWornItemsThenHairAndBeardWithVirtualSerials()
    {
        var mobiles = new MobileService(new StubMovementService(), TestSectors.Create());
        var aria = Aria(beard: 0x203E);

        var equipment = mobiles.GetEquipment(aria, [Worn(0x40000001, LayerType.Backpack)]);

        Assert.Equal([LayerType.Backpack, LayerType.Hair, LayerType.FacialHair], equipment.Select(entry => entry.Layer));
        Assert.Equal(new Serial(0x40000001), equipment[0].Serial);
        Assert.Equal((mobiles.HairSerial(aria.Id), 0x203C, (ushort)0x044E), (equipment[1].Serial, equipment[1].ItemId, equipment[1].Hue.Value));
        Assert.Equal((mobiles.BeardSerial(aria.Id), 0x203E), (equipment[2].Serial, equipment[2].ItemId));
    }

    [Fact]
    public void GetEquipment_NoHairOrBeardStyle_AddsNoEntryForThem()
    {
        var equipment = new MobileService(new StubMovementService(), TestSectors.Create()).GetEquipment(Aria(hair: 0), []);

        Assert.Empty(equipment);
    }

    [Fact]
    public void GetEquipment_AnItemOnTheHairLayer_TakesItsPlace()
    {
        var equipment = new MobileService(new StubMovementService(), TestSectors.Create()).GetEquipment(Aria(), [Worn(0x40000005, LayerType.Hair)]);

        Assert.Equal(new Serial(0x40000005), Assert.Single(equipment).Serial);
    }

    [Fact]
    public void GetEquipment_TwoItemsOnOneLayer_KeepsTheFirstAndSkipsUnworn()
    {
        var unworn = new ItemEntity { Id = new(0x40000009), TemplateId = "gold", ItemId = 0x0EED, Amount = 1 };

        var equipment = new MobileService(new StubMovementService(), TestSectors.Create()).GetEquipment(
            Aria(hair: 0),
            [Worn(0x40000001, LayerType.Shirt), Worn(0x40000002, LayerType.Shirt), unworn]
        );

        Assert.Equal(new Serial(0x40000001), Assert.Single(equipment).Serial);
    }

    [Fact]
    public void EnterWorld_PutsTheMobileInTheSectorGrid()
    {
        var sectors = TestSectors.Create();
        var mobiles = new MobileService(new StubMovementService(), sectors);
        var aria = Aria();

        mobiles.EnterWorld(aria);

        Assert.Equal([aria], sectors.GetMobilesInRange(aria.Map, aria.Location, 18));
    }

    [Fact]
    public void EnterWorld_TheSameSerialAgain_KeepsOneEntryInTheGrid()
    {
        var sectors = TestSectors.Create();
        var mobiles = new MobileService(new StubMovementService(), sectors);
        var first = Aria();
        var second = Aria();

        mobiles.EnterWorld(first);
        mobiles.EnterWorld(second);

        Assert.Equal([second], sectors.GetMobilesInRange(second.Map, second.Location, 18));
    }

    [Fact]
    public void TryMove_AStep_MovesTheMobileInTheGrid()
    {
        var sectors = TestSectors.Create();
        var mobiles = new MobileService(new StubMovementService(), sectors);
        var aria = Aria();
        aria.Direction = DirectionType.East;
        mobiles.EnterWorld(aria);
        var start = aria.Location;

        for (var step = 0; step < 40; step++)
        {
            Assert.Equal(MoveResultType.Moved, mobiles.TryMove(aria, DirectionType.East));
        }

        Assert.Empty(sectors.GetMobilesInRange(aria.Map, start, 18));
        Assert.Equal([aria], sectors.GetMobilesInRange(aria.Map, aria.Location, 18));
    }

    [Fact]
    public void LeaveWorld_TakesTheMobileOutOfTheGrid()
    {
        var sectors = TestSectors.Create();
        var mobiles = new MobileService(new StubMovementService(), sectors);
        var aria = Aria();
        mobiles.EnterWorld(aria);

        mobiles.LeaveWorld(aria.Id);

        Assert.Empty(sectors.GetMobilesInRange(aria.Map, aria.Location, 18));
    }

    [Theory]
    [InlineData(GenderType.Female, MobileFlagsType.Female)]
    [InlineData(GenderType.Male, MobileFlagsType.None)]
    public void GetFlags_FollowsTheGender(GenderType gender, MobileFlagsType expected)
    {
        var mobiles = new MobileService(new StubMovementService(), TestSectors.Create());
        var aria = Aria();
        aria.Gender = gender;

        Assert.Equal(expected, mobiles.GetFlags(aria));
    }

    [Fact]
    public void Delete_LeavesTheWorldAndQueuesTheRowForTheSave()
    {
        var sectors = TestSectors.Create();
        var mobiles = new MobileService(new StubMovementService(), sectors);
        var aria = Aria();
        mobiles.EnterWorld(aria);

        Assert.True(mobiles.Delete(aria.Id));

        Assert.False(mobiles.IsInWorld(aria.Id));
        Assert.Empty(sectors.GetMobilesInRange(aria.Map, aria.Location, 18));
        Assert.Equal([aria.Id], mobiles.Capture());
        Assert.False(mobiles.Delete(aria.Id));
    }

    [Fact]
    public void Committed_ForgetsTheSavedDeletions()
    {
        var mobiles = new MobileService(new StubMovementService(), TestSectors.Create());
        var aria = Aria();
        mobiles.EnterWorld(aria);
        mobiles.Delete(aria.Id);

        mobiles.Committed(mobiles.Capture());

        Assert.Empty(mobiles.Capture());
    }

    [Fact]
    public void EnterWorld_AfterDelete_CancelsTheDeletion()
    {
        var mobiles = new MobileService(new StubMovementService(), TestSectors.Create());
        var aria = Aria();
        mobiles.EnterWorld(aria);
        mobiles.Delete(aria.Id);

        mobiles.EnterWorld(aria);

        Assert.Empty(mobiles.Capture());
    }

    private static MobileEntity Aria(int hair = 0x203C, int beard = 0)
    {
        return new()
        {
            Id = new(2), AccountId = new Serial(42), Name = "Aria", Gender = GenderType.Female, Race = RaceType.Elf,
            HairStyle = hair, HairHue = new(0x044E), BeardStyle = beard, BeardHue = new(0x044E), Hits = 60, HitsMax = 61,
            Strength = 62, Dexterity = 20, Intelligence = 10, Stamina = 21, StaminaMax = 22, Mana = 11, ManaMax = 12,
            ResistPhysical = 1, ResistFire = 2, ResistCold = 3, ResistPoison = 4, ResistEnergy = 5,
            Map = MapType.Trammel, Location = new Point3D(1496, 1628, 10)
        };
    }

    private static ItemEntity Worn(uint serial, LayerType layer)
    {
        var item = new ItemEntity { Id = new(serial), TemplateId = "worn", ItemId = 0x1517, Amount = 1 };
        item.Equip(new Serial(2), layer);

        return item;
    }
}
