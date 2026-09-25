using Moongate.Server.Ultima.Types.Characters;

namespace Moongate.Tests.Server.Ultima.Types.Characters;

public sealed class CharacterListFlagsTests
{
    [Theory]
    [InlineData(CharacterListFlags.ExpansionLbr, 0x00000008u)]
    [InlineData(CharacterListFlags.ExpansionAos, 0x00000028u)]
    [InlineData(CharacterListFlags.ExpansionSe, 0x000000A8u)]
    [InlineData(CharacterListFlags.ExpansionMl, 0x000001A8u)]
    public void Expansion_MatchesTheFlagsSentToTheClient(CharacterListFlags flags, uint expected)
    {
        Assert.Equal(expected, (uint)flags);
    }

    [Fact]
    public void Expansion_AfterMondainsLegacy_AddsNoFlags()
    {
        CharacterListFlags[] lateExpansions =
        [
            CharacterListFlags.ExpansionSa,
            CharacterListFlags.ExpansionHs,
            CharacterListFlags.ExpansionTol,
            CharacterListFlags.ExpansionEj
        ];

        Assert.All(lateExpansions, flags => Assert.Equal(CharacterListFlags.ExpansionMl, flags));
    }

    [Fact]
    public void Expansion_BeforeAgeOfShadows_OnlyHasContextMenus()
    {
        CharacterListFlags[] earlyExpansions =
        [
            CharacterListFlags.ExpansionNone,
            CharacterListFlags.ExpansionT2A,
            CharacterListFlags.ExpansionUor,
            CharacterListFlags.ExpansionUotd,
            CharacterListFlags.ExpansionLbr
        ];

        Assert.All(earlyExpansions, flags => Assert.Equal(CharacterListFlags.ContextMenus, flags));
    }

    [Fact]
    public void Default_IsTheNewestExpansionWithoutSlotFlags()
    {
        var flags = CharacterListFlags.Default;

        Assert.Equal(0x000001A8u, (uint)flags);
        Assert.False(flags.HasFlag(CharacterListFlags.SixthCharacterSlot));
        Assert.False(flags.HasFlag(CharacterListFlags.SeventhCharacterSlot));
    }

    [Fact]
    public void ExpansionFlags_CombineWithSlotFlags()
    {
        var flags = CharacterListFlags.Default | CharacterListFlags.SixthCharacterSlot;

        Assert.Equal(0x000001E8u, (uint)flags);
        Assert.True(flags.HasFlag(CharacterListFlags.ContextMenus));
        Assert.False(flags.HasFlag(CharacterListFlags.SeventhCharacterSlot));
    }

    [Fact]
    public void SlotFlags_UseTheCharacterListBitLayoutNotTheFeatureFlagsOne()
    {
        Assert.Equal(0x00000040u, (uint)CharacterListFlags.SixthCharacterSlot);
        Assert.Equal(0x00001000u, (uint)CharacterListFlags.SeventhCharacterSlot);
    }
}
