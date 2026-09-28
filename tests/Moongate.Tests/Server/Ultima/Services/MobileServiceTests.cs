using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class MobileServiceTests
{
    [Fact]
    public void HairAndBeardSerials_AreVirtualDistinctAndStablePerMobile()
    {
        var mobiles = new MobileService();
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
        var mobiles = new MobileService();

        Assert.NotEqual(mobiles.HairSerial(new Serial(2)), mobiles.HairSerial(new Serial(3)));
    }

    [Fact]
    public void FirstVirtualSerial_IsTheStartOfTheVirtualRange()
    {
        Assert.Equal(new Serial(Serial.MinVirtual), new MobileService().HairSerial(new Serial(2)));
    }

    [Fact]
    public void EnterWorld_MarksTheMobileInTheWorld()
    {
        var mobiles = new MobileService();
        var aria = new Serial(2);

        mobiles.EnterWorld(aria);

        Assert.True(mobiles.IsInWorld(aria));
        Assert.False(mobiles.IsInWorld(new Serial(3)));
        Assert.Equal([aria], mobiles.InWorld);
    }

    [Fact]
    public void GetStatus_MapsTheMobileWithTheStatCapAndFollowersMax()
    {
        var status = new MobileService().GetStatus(Aria());

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
        var mobiles = new MobileService();
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
        var equipment = new MobileService().GetEquipment(Aria(hair: 0), []);

        Assert.Empty(equipment);
    }

    [Fact]
    public void GetEquipment_AnItemOnTheHairLayer_TakesItsPlace()
    {
        var equipment = new MobileService().GetEquipment(Aria(), [Worn(0x40000005, LayerType.Hair)]);

        Assert.Equal(new Serial(0x40000005), Assert.Single(equipment).Serial);
    }

    [Fact]
    public void GetEquipment_TwoItemsOnOneLayer_KeepsTheFirstAndSkipsUnworn()
    {
        var unworn = new ItemEntity { Id = new(0x40000009), TemplateId = "gold", ItemId = 0x0EED, Amount = 1 };

        var equipment = new MobileService().GetEquipment(
            Aria(hair: 0),
            [Worn(0x40000001, LayerType.Shirt), Worn(0x40000002, LayerType.Shirt), unworn]
        );

        Assert.Equal(new Serial(0x40000001), Assert.Single(equipment).Serial);
    }

    private static MobileEntity Aria(int hair = 0x203C, int beard = 0)
    {
        return new()
        {
            Id = new(2), AccountId = new Serial(42), Name = "Aria", Gender = GenderType.Female, Race = RaceType.Elf,
            HairStyle = hair, HairHue = new(0x044E), BeardStyle = beard, BeardHue = new(0x044E), Hits = 60, HitsMax = 61,
            Strength = 62, Dexterity = 20, Intelligence = 10, Stamina = 21, StaminaMax = 22, Mana = 11, ManaMax = 12,
            ResistPhysical = 1, ResistFire = 2, ResistCold = 3, ResistPoison = 4, ResistEnergy = 5
        };
    }

    private static ItemEntity Worn(uint serial, LayerType layer)
    {
        var item = new ItemEntity { Id = new(serial), TemplateId = "worn", ItemId = 0x1517, Amount = 1 };
        item.Equip(new Serial(2), layer);

        return item;
    }
}
