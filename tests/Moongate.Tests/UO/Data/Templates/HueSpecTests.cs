using Moongate.UO.Data.Templates.Items;

namespace Moongate.Tests.UO.Data.Templates;

public class HueSpecTests
{
    [Test]
    public void ParseFromString_ShouldParseHexValue()
    {
        var hue = HueSpec.ParseFromString("0x04A2");

        Assert.Multiple(
            () =>
            {
                Assert.That(hue.IsRange, Is.False);
                Assert.That(hue.Min, Is.EqualTo(0x04A2));
                Assert.That(hue.Max, Is.EqualTo(0x04A2));
            }
        );
    }

    [Test]
    public void ParseFromString_ShouldParseNumericValue()
    {
        var hue = HueSpec.ParseFromString("1186");

        Assert.Multiple(
            () =>
            {
                Assert.That(hue.IsRange, Is.False);
                Assert.That(hue.Min, Is.EqualTo(1186));
                Assert.That(hue.Max, Is.EqualTo(1186));
            }
        );
    }

    [Test]
    public void ParseFromString_ShouldParseRangeValue()
    {
        var hue = HueSpec.ParseFromString("hue(100:200)");

        Assert.Multiple(
            () =>
            {
                Assert.That(hue.IsRange, Is.True);
                Assert.That(hue.Min, Is.EqualTo(100));
                Assert.That(hue.Max, Is.EqualTo(200));
            }
        );
    }

    [Test]
    public void ParseFromString_ShouldThrow_OnInvalidValue()
    {
        Assert.That(
            () => HueSpec.ParseFromString("random(10:20)"),
            Throws.TypeOf<FormatException>()
        );
    }
}
