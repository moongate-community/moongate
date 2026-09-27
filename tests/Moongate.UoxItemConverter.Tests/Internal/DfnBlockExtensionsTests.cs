using Moongate.UoxItemConverter.Internal;

namespace Moongate.UoxItemConverter.Tests.Internal;

public sealed class DfnBlockExtensionsTests
{
    [Fact]
    public void GetTargets_SplitsARandomPick_AndIsEmptyWithoutGet()
    {
        Assert.Equal(["graydragon", "reddragon"], Block("GET=graydragon  reddragon").GetTargets());
        Assert.Empty(Block("NAME=an orc").GetTargets());
    }

    [Fact]
    public void ParentTargets_PreferGetLbr_OverGet()
    {
        Assert.Equal(["smallbod_oldid"], Block("get=base_item", "getlbr=smallbod_oldid").ParentTargets());
        Assert.Equal(["base_item"], Block("get=base_item").ParentTargets());
    }

    private static DfnBlock Block(params string[] lines)
    {
        return Assert.Single(DfnParser.Parse(["[x]", "{", ..lines, "}"]));
    }
}
