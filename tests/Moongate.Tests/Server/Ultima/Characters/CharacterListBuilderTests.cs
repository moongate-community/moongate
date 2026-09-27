using Moongate.Core.Types.Expansions;
using Moongate.Server.Ultima.Characters;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Characters;

namespace Moongate.Tests.Server.Ultima.Characters;

public sealed class CharacterListBuilderTests
{
    [Fact]
    public void Names_PlacesEachCharacterAtItsSlot()
    {
        var names = CharacterListBuilder.Names([Character("A", 0), Character("B", 3)], 7);

        Assert.Equal(["A", null, null, "B", null, null, null], names);
    }

    [Fact]
    public void Names_CharacterBeyondTheLimit_TakesTheFirstFreeSlot()
    {
        var names = CharacterListBuilder.Names([Character("Far", 6), Character("A", 0)], 5);

        Assert.Equal(["A", "Far", null, null, null], names);
    }

    [Fact]
    public void Names_CharacterWithoutSlot_TakesTheFirstFreeSlot()
    {
        var names = CharacterListBuilder.Names([Character("Old", null), Character("A", 0)], 5);

        Assert.Equal(["A", "Old", null, null, null], names);
    }

    [Fact]
    public void Names_TwoCharactersInOneSlot_KeepsBoth()
    {
        var names = CharacterListBuilder.Names([Character("A", 1), Character("B", 1)], 5);

        Assert.Equal(["B", "A", null, null, null], names);
    }

    [Fact]
    public void Names_MoreCharactersThanSlots_DropsTheExtraOnes()
    {
        var names = CharacterListBuilder.Names([Character("A", 0), Character("B", 1)], 1);

        Assert.Equal(["A"], names);
    }

    [Theory]
    [InlineData(7, CharacterListFlags.SixthCharacterSlot | CharacterListFlags.SeventhCharacterSlot)]
    [InlineData(6, CharacterListFlags.SixthCharacterSlot)]
    [InlineData(5, CharacterListFlags.None)]
    [InlineData(1, CharacterListFlags.OneCharacterSlot | CharacterListFlags.SlotLimit)]
    public void SlotFlags_FollowTheLimit(int max, CharacterListFlags expected)
    {
        Assert.Equal(expected, CharacterListBuilder.SlotFlags(max));
    }

    [Theory]
    [InlineData(7, true, true)]
    [InlineData(6, true, false)]
    [InlineData(5, false, false)]
    public void Features_AddTheSlotFeaturesOfTheLimit(int max, bool sixth, bool seventh)
    {
        var features = CharacterListBuilder.Features(max);

        Assert.True(features.HasFlag(FeatureFlags.ExpansionEj));
        Assert.Equal(seventh, features.HasFlag(FeatureFlags.SeventhCharacterSlot));
        Assert.Equal(sixth || (FeatureFlags.ExpansionEj & FeatureFlags.SixthCharacterSlot) != 0, features.HasFlag(FeatureFlags.SixthCharacterSlot));
    }

    private static MobileEntity Character(string name, byte? slot)
    {
        return new() { Name = name, Slot = slot };
    }
}
