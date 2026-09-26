using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Tests.Support.Serialization.Types;

namespace Moongate.Tests.Core.Primitives;

public sealed class EnumValueSpecTests
{
    [Fact]
    public void FromValue_Resolve_AlwaysReturnsTheSameValue()
    {
        var spec = EnumValueSpec<TemplateRarity>.FromValue(TemplateRarity.Epic);

        Assert.False(spec.IsRandom);
        Assert.Equal(TemplateRarity.Epic, spec.Resolve());
        Assert.Equal(TemplateRarity.Epic, spec.Resolve());
    }

    [Fact]
    public void Random_Resolve_OnlyEverReturnsAMemberOfTheEnum()
    {
        var spec = EnumValueSpec<TemplateRarity>.Random();

        for (var i = 0; i < 50; i++)
        {
            Assert.True(Enum.IsDefined(spec.Resolve()));
        }
    }

    [Fact]
    public void FromCandidates_Resolve_OnlyEverReturnsOneOfTheCandidates()
    {
        var spec = EnumValueSpec<TemplateRarity>.FromCandidates([TemplateRarity.Rare, TemplateRarity.Epic]);

        for (var i = 0; i < 50; i++)
        {
            Assert.Contains(spec.Resolve(), (TemplateRarity[])[TemplateRarity.Rare, TemplateRarity.Epic]);
        }
    }

    [Fact]
    public void FromCandidates_WithNoCandidates_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => EnumValueSpec<TemplateRarity>.FromCandidates([]));
    }

    [Theory, InlineData("common", TemplateRarity.Common), InlineData("EPIC", TemplateRarity.Epic),
     InlineData("  rare  ", TemplateRarity.Rare)]
    public void TryParse_AMemberName_ParsesAFixedValue(string text, TemplateRarity expected)
    {
        Assert.True(EnumValueSpec<TemplateRarity>.TryParse(text, out var spec));
        Assert.False(spec.IsRandom);
        Assert.Equal(expected, spec.Resolve());
    }

    [Fact]
    public void TryParse_RandomOf_ParsesARandomPickAmongEveryMember()
    {
        Assert.True(EnumValueSpec<TemplateRarity>.TryParse("random_of", out var spec));
        Assert.True(EnumValueSpec<TemplateRarity>.TryParse("RANDOM_OF", out var caseInsensitive));

        Assert.True(spec.IsRandom);
        Assert.True(caseInsensitive.IsRandom);
    }

    [Fact]
    public void TryParse_RandomOfWithACandidateList_ParsesARandomPickAmongThoseMembers()
    {
        Assert.True(EnumValueSpec<TemplateRarity>.TryParse("random_of:rare,epic,legendary", out var spec));

        for (var i = 0; i < 50; i++)
        {
            Assert.Contains(
                spec.Resolve(),
                (TemplateRarity[])[TemplateRarity.Rare, TemplateRarity.Epic, TemplateRarity.Legendary]
            );
        }
    }

    [Theory, InlineData(null), InlineData(""), InlineData("   "), InlineData("not_a_member"), InlineData("random_of:"),
     InlineData("random_of:rare,not_a_member")]
    public void TryParse_InvalidText_ReturnsFalse(string? text)
    {
        Assert.False(EnumValueSpec<TemplateRarity>.TryParse(text, out var spec));
        Assert.False(spec.IsRandom);
    }

    [Fact]
    public void ToString_AFixedValue_WritesTheLowercaseMemberName()
    {
        var spec = EnumValueSpec<TemplateRarity>.FromValue(TemplateRarity.Epic);

        Assert.Equal("epic", spec.ToString());
    }

    [Fact]
    public void ToString_ARandomPick_WritesTheLowercaseCandidateList()
    {
        var spec = EnumValueSpec<TemplateRarity>.FromCandidates([TemplateRarity.Rare, TemplateRarity.Epic]);

        Assert.Equal("random_of:rare,epic", spec.ToString());
    }

    [Fact]
    public void ToString_ThenTryParse_RoundTrips()
    {
        var original = EnumValueSpec<TemplateRarity>.FromCandidates([TemplateRarity.Common, TemplateRarity.Legendary]);

        Assert.True(EnumValueSpec<TemplateRarity>.TryParse(original.ToString(), out var restored));

        Assert.Equal(original.ToString(), restored.ToString());
    }

    [Theory, InlineData("north_east"), InlineData("NorthEast"), InlineData("northeast"), InlineData("NORTH_EAST")]
    public void TryParse_AMultiWordName_AcceptsTheSnakeCaseFormItWrites(string text)
    {
        Assert.True(EnumValueSpec<DirectionType>.TryParse(text, out var spec));
        Assert.Equal(DirectionType.NorthEast, spec.Resolve());
    }

    [Fact]
    public void ToString_ThenTryParse_RoundTripsMultiWordNames()
    {
        var fixedSpec = EnumValueSpec<DirectionType>.FromValue(DirectionType.SouthWest);
        var randomSpec = EnumValueSpec<DirectionType>.FromCandidates([DirectionType.NorthEast, DirectionType.SouthWest]);

        Assert.True(EnumValueSpec<DirectionType>.TryParse(fixedSpec.ToString(), out var fixedBack));
        Assert.True(EnumValueSpec<DirectionType>.TryParse(randomSpec.ToString(), out var randomBack));
        Assert.Equal(DirectionType.SouthWest, fixedBack.Resolve());
        Assert.Equal(randomSpec.ToString(), randomBack.ToString());
    }

    [Theory, InlineData("99"), InlineData("1"), InlineData("-1"), InlineData("random_of:1,2"), InlineData("north__east_x")]
    public void TryParse_ANumberOrAnUnknownName_ReturnsFalse(string text)
    {
        Assert.False(EnumValueSpec<DirectionType>.TryParse(text, out _));
    }
}
