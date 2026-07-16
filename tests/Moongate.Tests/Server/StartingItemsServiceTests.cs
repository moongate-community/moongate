using Moongate.Server.Services.World;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class StartingItemsServiceTests
{
    [Fact]
    public void Resolve_MergesAllBodyAndTopSkills()
    {
        var kit = Service().Resolve(RaceType.Human, GenderType.Male, ["Blacksmithy", "Magery"]);

        Assert.Contains(kit.Pack, e => e.Item == "dagger");     // All
        Assert.Contains(kit.Equip, e => e.Item == "shirt");     // ByBody Human/Male
        Assert.Contains(kit.Pack, e => e.Item == "iron_ingot"); // BySkill Blacksmithy
        Assert.Contains(kit.Equip, e => e.Item == "robe");      // BySkill Magery
    }

    [Fact]
    public void Resolve_UnknownBodyAndSkill_ContributeNothing()
    {
        var kit = Service().Resolve(RaceType.Gargoyle, GenderType.Female, ["Nonexistent"]);

        Assert.Single(kit.Pack); // only All's dagger
        Assert.Empty(kit.Equip);
        Assert.Equal("dagger", kit.Pack[0].Item);
    }

    private static StartingItemsService Service()
    {
        var service = new StartingItemsService();
        service.Load(
            new()
            {
                All = new() { Pack = [new() { Item = "dagger" }] },
                ByBody =
                {
                    ["Human/Male"] = new() { Equip = [new() { Item = "shirt" }] }
                },
                BySkill =
                {
                    ["Blacksmithy"] = new() { Pack = [new() { Item = "iron_ingot", Amount = 50 }] },
                    ["Magery"] = new() { Equip = [new() { Item = "robe" }] }
                }
            }
        );

        return service;
    }
}
