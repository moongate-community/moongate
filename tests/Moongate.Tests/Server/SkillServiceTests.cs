using Moongate.Server.Services.Mobiles;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class SkillServiceTests
{
    [Fact]
    public void All_IsOrderedById()
    {
        var service = NewService();
        service.Register(Def(2, "Animal Lore"));
        service.Register(Def(0, "Alchemy"));
        service.Register(Def(1, "Anatomy"));

        Assert.Equal(new[] { 0, 1, 2 }, service.All.Select(d => d.Id).ToArray());
    }

    [Fact]
    public void GetByName_IsCaseInsensitive()
    {
        var service = NewService();
        service.Register(Def(0, "Alchemy"));

        Assert.Equal(0, service.GetByName("alchemy")!.Id);
    }

    [Fact]
    public void Lookup_UnknownKey_ReturnsNull()
    {
        var service = NewService();

        Assert.Null(service.GetById(999));
        Assert.Null(service.GetByName("Nope"));
    }

    [Fact]
    public void Register_ThenLookup_FindsByIdAndName()
    {
        var service = NewService();
        service.Register(Def(0, "Alchemy"));
        service.Register(Def(1, "Anatomy"));

        Assert.Equal(2, service.Count);
        Assert.Equal("Alchemy", service.GetById(0)!.Name);
        Assert.Equal(1, service.GetByName("Anatomy")!.Id);
    }

    private static SkillDefinition Def(int id, string name)
        => new() { Id = id, Name = name, PrimaryStat = StatType.Int, SecondaryStat = StatType.Dex };

    private static SkillService NewService()
        => new();
}
