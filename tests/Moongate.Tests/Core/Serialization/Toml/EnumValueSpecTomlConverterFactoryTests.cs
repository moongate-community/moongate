using System.Text.Json;
using Moongate.Core.Primitives;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Tests.Support.Serialization.Data;
using Moongate.Tests.Support.Serialization.Types;
using Tomlyn;

namespace Moongate.Tests.Core.Serialization.Toml;

public sealed class EnumValueSpecTomlConverterFactoryTests
{
    private static readonly TomlSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = [new EnumValueSpecTomlConverterFactory()]
    };

    [Fact]
    public void Deserialize_AFixedMemberName_ResolvesToThatMember()
    {
        var holder = TomlUtils.Deserialize<TemplateRarityHolder>("rarity = \"epic\"\n", Options);

        Assert.False(holder!.Rarity.IsRandom);
        Assert.Equal(TemplateRarity.Epic, holder.Rarity.Resolve());
    }

    [Fact]
    public void Deserialize_RandomOf_ResolvesToAnyMember()
    {
        var holder = TomlUtils.Deserialize<TemplateRarityHolder>("rarity = \"random_of\"\n", Options);

        Assert.True(holder!.Rarity.IsRandom);
        Assert.True(Enum.IsDefined(holder.Rarity.Resolve()));
    }

    [Fact]
    public void Deserialize_RandomOfWithACandidateList_ResolvesToOnlyThoseMembers()
    {
        var holder = TomlUtils.Deserialize<TemplateRarityHolder>("rarity = \"random_of:rare,legendary\"\n", Options);

        for (var i = 0; i < 50; i++)
        {
            Assert.Contains(
                holder!.Rarity.Resolve(),
                (TemplateRarity[])[TemplateRarity.Rare, TemplateRarity.Legendary]
            );
        }
    }

    [Fact]
    public void Deserialize_AnInvalidSpec_ThrowsTomlExceptionNamingTheText()
    {
        var exception = Assert.Throws<TomlException>(
            () => TomlUtils.Deserialize<TemplateRarityHolder>("rarity = \"not-a-member\"\n", Options)
        );

        Assert.Contains("not-a-member", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_AFixedValue_WritesTheLowercaseMemberName()
    {
        var toml = TomlUtils.Serialize(
            new TemplateRarityHolder { Rarity = EnumValueSpec<TemplateRarity>.FromValue(TemplateRarity.Rare) },
            Options
        );

        Assert.Equal("rarity = \"rare\"", toml.Trim());
    }

    [Fact]
    public void Serialize_ARandomPick_WritesTheRandomOfCandidateList()
    {
        var toml = TomlUtils.Serialize(
            new TemplateRarityHolder
            {
                Rarity = EnumValueSpec<TemplateRarity>.FromCandidates([TemplateRarity.Rare, TemplateRarity.Epic])
            },
            Options
        );

        Assert.Equal("rarity = \"random_of:rare,epic\"", toml.Trim());
    }

    [Fact]
    public void RoundTrip_APlainEnumProperty_StillUsesTomlynsOwnConverter()
    {
        // The factory only claims EnumValueSpec<TEnum>; a bare enum property elsewhere in the same
        // document is untouched and still works through Tomlyn's built-in enum converter.
        var toml = TomlUtils.Deserialize<PlainRarityHolder>("rarity = \"Epic\"\n", Options);

        Assert.Equal(TemplateRarity.Epic, toml!.Rarity);
    }
}
