using MoonSharp.Interpreter;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Scripting;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class MobileModuleTests
{
    [Fact]
    public void Create_PersistsAndReturnsSerial()
    {
        var (module, persistence) = Build();

        var serial = module.Create("Guard", 1, 100, 200, 5);

        Assert.NotNull(serial);
        var stored = persistence.Store<MobileEntity>().GetById((Serial)serial!.Value)!;
        Assert.Equal("Guard", stored.Name);
        Assert.Equal(1, stored.MapId);
        Assert.Equal(100, stored.Position.X);
        Assert.Equal(5, stored.Position.Z);
    }

    [Fact]
    public void Get_ReturnsFieldTable()
    {
        var (module, _) = Build();
        var serial = module.Create("Guard", 1, 100, 200, 5)!.Value;

        var table = module.Get(serial);

        Assert.NotNull(table);
        Assert.Equal("Guard", table!["name"]);
        Assert.Equal(100, table["x"]);
        Assert.Equal(1, table["map"]);
    }

    [Fact]
    public void Get_UnknownSerial_ReturnsNull()
    {
        var (module, _) = Build();
        Assert.Null(module.Get(999999u));
    }

    [Fact]
    public void Set_AppliesStatsGenderRaceAndPersists()
    {
        var (module, persistence) = Build();
        var serial = module.Create("Guard", 1, 100, 200, 5)!.Value;

        var fields = new Table(new Script());
        fields["str"] = 80;
        fields["dex"] = 60;
        fields["int"] = 40;
        fields["gender"] = "Female";
        fields["race"] = "Elf";
        fields["skin_hue"] = 1002;

        Assert.True(module.Set(serial, fields));

        var m = persistence.Store<MobileEntity>().GetById((Serial)serial)!;
        Assert.Equal(80, m.Strength);
        Assert.Equal(GenderType.Female, m.Gender);
        Assert.Equal(RaceType.Elf, m.Race);
        Assert.Equal((ushort)1002, m.SkinHue.Value);
    }

    [Fact]
    public void Set_AcceptsNumericGenderAndRaceConstants()
    {
        var (module, persistence) = Build();
        var serial = module.Create("Guard", 1, 0, 0, 0)!.Value;

        var fields = new Table(new Script());
        fields["gender"] = (int)GenderType.Female;
        fields["race"] = (int)RaceType.Gargoyle;

        Assert.True(module.Set(serial, fields));

        var m = persistence.Store<MobileEntity>().GetById((Serial)serial)!;
        Assert.Equal(GenderType.Female, m.Gender);
        Assert.Equal(RaceType.Gargoyle, m.Race);
    }

    [Fact]
    public void Set_InvalidRace_IgnoresField()
    {
        var (module, persistence) = Build();
        var serial = module.Create("Guard", 1, 100, 200, 5)!.Value;
        var before = persistence.Store<MobileEntity>().GetById((Serial)serial)!.Race;

        var fields = new Table(new Script());
        fields["race"] = "Dragonkin";

        Assert.True(module.Set(serial, fields));
        Assert.Equal(before, persistence.Store<MobileEntity>().GetById((Serial)serial)!.Race);
    }

    [Fact]
    public void Move_UpdatesPosition()
    {
        var (module, persistence) = Build();
        var serial = module.Create("Guard", 1, 100, 200, 5)!.Value;

        Assert.True(module.Move(serial, 10, 20, 0));

        var m = persistence.Store<MobileEntity>().GetById((Serial)serial)!;
        Assert.Equal(10, m.Position.X);
        Assert.Equal(20, m.Position.Y);
        Assert.Equal(1, m.MapId);
    }

    [Fact]
    public void Skills_SetGetAndList_ByName()
    {
        var (module, _) = Build();
        var serial = module.Create("Guard", 1, 100, 200, 5)!.Value;

        Assert.True(module.SetSkill(serial, "Swordsmanship", 550));
        Assert.Equal(550, module.GetSkill(serial, "Swordsmanship"));
        Assert.Equal(0, module.GetSkill(serial, "Tactics"));
        Assert.Equal(550, module.Skills(serial)!["Swordsmanship"]);
    }

    [Fact]
    public void SetSkill_AcceptsDisplayNameWithSpaces()
    {
        var (module, _) = Build();
        var serial = module.Create("Tamer", 1, 0, 0, 0)!.Value;

        Assert.True(module.SetSkill(serial, "Animal Lore", 300));
        Assert.Equal(300, module.GetSkill(serial, "AnimalLore"));
    }

    [Fact]
    public void SetSkill_UnknownSkillName_ReturnsFalse()
    {
        var (module, _) = Build();
        var serial = module.Create("Guard", 1, 0, 0, 0)!.Value;

        Assert.False(module.SetSkill(serial, "Jumping", 100));
        Assert.Equal(0, module.GetSkill(serial, "Jumping"));
    }

    [Fact]
    public void SetSkill_AcceptsNumericId_AndUnifiesWithName()
    {
        var (module, _) = Build();
        var serial = module.Create("Guard", 1, 0, 0, 0)!.Value;

        // Lua passes an exposed SkillName constant as a number (double). 40 == Swordsmanship.
        Assert.True(module.SetSkill(serial, 40d, 700));

        Assert.Equal(700, module.GetSkill(serial, 40d));
        Assert.Equal(700, module.GetSkill(serial, "Swordsmanship"));
    }

    [Fact]
    public void Delete_RemovesMobile()
    {
        var (module, persistence) = Build();
        var serial = module.Create("Guard", 1, 100, 200, 5)!.Value;

        Assert.True(module.Delete(serial));
        Assert.Null(persistence.Store<MobileEntity>().GetById((Serial)serial));
    }

    private static (MobileModule Module, FakePersistenceService Persistence) Build()
    {
        var persistence = new FakePersistenceService();
        return (new MobileModule(persistence), persistence);
    }
}
