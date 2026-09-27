using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Tests.Server.Ultima.Entities.World;

public sealed class MobileEntityTests
{
    [Fact]
    public void Props_SetGetConvertAndRemove()
    {
        var mobile = new MobileEntity();

        mobile.SetProp("quest_step", 3);
        mobile.SetProp("title_color", NotorietyType.Criminal);

        Assert.Equal(3, mobile.GetProp<int>("quest_step"));
        Assert.Equal(NotorietyType.Criminal, mobile.GetProp<NotorietyType>("title_color"));
        Assert.Equal("none", mobile.GetProp("missing", "none"));
        Assert.True(mobile.RemoveProp("quest_step"));
        mobile.SetProp("title_color", null);
        Assert.Null(mobile.Props);
    }

    [Fact]
    public void Props_ANestedValue_IsRejected_AndABadConversionThrows()
    {
        var mobile = new MobileEntity { Props = new() { ["hits"] = "many" } };

        Assert.Throws<ArgumentException>(() => mobile.SetProp("list", new List<int>()));
        Assert.Contains("'hits'", Assert.Throws<InvalidCastException>(() => mobile.GetProp<int>("hits")).Message);
    }
}
