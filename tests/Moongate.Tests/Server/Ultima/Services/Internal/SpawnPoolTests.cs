using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Spawns;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Tests.TestSupport.Random;

namespace Moongate.Tests.Server.Ultima.Services.Internal;

public sealed class SpawnPoolTests
{
    private static readonly Dictionary<string, NpcListTemplate> Lists = new()
    {
        ["orcs"] = new()
        {
            Id = "orcs",
            Entries = [new() { Weight = 3, MobileId = "orc" }, new() { Weight = 1, NpcListId = "trolls" }]
        },
        ["trolls"] = new()
        {
            Id = "trolls",
            Entries = [new() { MobileId = "troll" }, new() { Weight = 2, MobileId = "frost_troll" }]
        }
    };

    [Theory, InlineData(0, "rabbit"), InlineData(1, "deer")]
    public void Pick_MobilesWeighOneEach(int roll, string expected)
    {
        var pool = new SpawnPool(new() { MobileIds = ["rabbit", "deer"] }, Lists);

        Assert.Equal(expected, pool.Pick(new ScriptedRandom(roll)));
    }

    [Theory, InlineData(0, "orc"), InlineData(2, "orc"), InlineData(3, "troll")]
    public void Pick_ListEntriesWeighTheirWeight_AndANestedListPicksFromItself(int roll, string expected)
    {
        var pool = new SpawnPool(new() { NpcListIds = ["orcs"] }, Lists);

        Assert.Equal(expected, pool.Pick(new ScriptedRandom(roll, 0)));
    }

    [Fact]
    public void Pick_ANestedListRollsItsOwnWeights()
    {
        var pool = new SpawnPool(new() { NpcListIds = ["orcs"] }, Lists);

        Assert.Equal("frost_troll", pool.Pick(new ScriptedRandom(3, 1)));
    }

    [Fact]
    public void Pick_MobilesAndListsShareOnePool()
    {
        // rabbit weighs 1, then the entries of orcs: orc 3, trolls 1.
        var pool = new SpawnPool(new() { MobileIds = ["rabbit"], NpcListIds = ["orcs"] }, Lists);

        Assert.Equal("rabbit", pool.Pick(new ScriptedRandom(0)));
        Assert.Equal("orc", pool.Pick(new ScriptedRandom(1)));
        Assert.Equal("troll", pool.Pick(new ScriptedRandom(4, 0)));
    }
}
