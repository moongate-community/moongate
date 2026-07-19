using Moongate.UO.Data.Mobiles.Templates;

namespace Moongate.Tests.UO.Data.Mobiles.Templates;

public class HueSpecTests
{
    [Theory, InlineData(null, 0), InlineData("", 0), InlineData("1002", 1002), InlineData("garbage", 0)]
    public void Resolve_PlainAndInvalid(string? spec, int expected)
        => Assert.Equal((ushort)expected, HueSpec.Resolve(spec, new(1)));

    [Fact]
    public void Resolve_Range_IsWithinInclusiveBounds()
    {
        var rng = new Random(1);

        for (var i = 0; i < 50; i++)
        {
            var hue = HueSpec.Resolve("hue(1002:1058)", rng);
            Assert.InRange(hue, (ushort)1002, (ushort)1058);
        }
    }
}
