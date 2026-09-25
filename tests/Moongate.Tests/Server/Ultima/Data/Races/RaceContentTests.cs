using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Races;

namespace Moongate.Tests.Server.Ultima.Data.Races;

public sealed class RaceContentTests
{
    private static readonly RaceContent Elf = new()
    {
        SkinHues = [HueSpec.FromValue(0x00BF), HueSpec.FromValue(0x024D), HueSpec.FromRange(0x0381, 0x0385)],
        HairHues = []
    };

    [Theory, InlineData(0x024D, 0x024D), InlineData(0x0383, 0x0383), InlineData(0x0250, 0x024D), InlineData(0x0390, 0x0385), InlineData(0x0010, 0x00BF)]
    public void ClipSkinHue_ReturnsTheHueOrTheNearestAllowedOne(int sent, int expected)
    {
        Assert.Equal(new Hue((ushort)expected), Elf.ClipSkinHue(new((ushort)sent)));
    }

    [Fact]
    public void ClipSkinHue_IgnoresTheFlagBits()
    {
        Assert.Equal(new Hue(0x024D), Elf.ClipSkinHue(new(0x824D)));
    }

    [Fact]
    public void ClipHairHue_WithNoAllowedHues_KeepsAnyHue()
    {
        Assert.Equal(new Hue(0x0123), Elf.ClipHairHue(new(0x0123)));
    }
}
