using Moongate.Server.Ultima.Data.Crafts;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class CraftServiceTests
{
    private readonly CraftService _crafts = new(
        new StubDataLoaderService()
            .With(new CraftDefinition { Id = "carpentry", Name = "Carpentry", Skill = "carpentry" })
            .With(new CraftResourceList { Id = "wood", Templates = ["0x1bd7_board", "0x1bda_board"] })
    );

    [Fact]
    public void Get_GivesTheCraftOfThatId_AndNothingForAnother()
    {
        Assert.Equal("Carpentry", _crafts.Get("carpentry")?.Name);
        Assert.Null(_crafts.Get("tailoring"));
    }

    [Fact]
    public void Resource_GivesTheTemplatesOfTheList_AndNothingForAnother()
    {
        Assert.Equal(["0x1bd7_board", "0x1bda_board"], _crafts.Resource("wood"));
        Assert.Null(_crafts.Resource("gems"));
    }
}
