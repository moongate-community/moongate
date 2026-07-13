using Moongate.Server.Services.Mobiles;
using Moongate.UO.Data.Professions;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class ProfessionServiceTests
{
    private static ProfessionDefinition Def(string name)
    {
        return new ProfessionDefinition
        {
            Name = name,
            TrueName = name,
            Type = "Profession",
            Skills = [new ProfessionSkill { Name = "Magery", Value = 30 }],
            Stats = [new ProfessionStat { Type = StatType.Int, Value = 45 }]
        };
    }

    [Fact]
    public void Register_ThenLookup_IsCaseInsensitive()
    {
        var service = new ProfessionService();
        service.Register(Def("Mage"));

        Assert.Equal(1, service.Count);
        Assert.Equal("Mage", service.GetByName("mage")!.Name);
        Assert.Equal(StatType.Int, service.GetByName("Mage")!.Stats[0].Type);
    }

    [Fact]
    public void Lookup_Unknown_ReturnsNull()
    {
        var service = new ProfessionService();

        Assert.Null(service.GetByName("Nope"));
    }

    [Fact]
    public void All_IsOrderedByName()
    {
        var service = new ProfessionService();
        service.Register(Def("Warrior"));
        service.Register(Def("Mage"));

        Assert.Equal(new[] { "Mage", "Warrior" }, service.All.Select(p => p.Name).ToArray());
    }
}
