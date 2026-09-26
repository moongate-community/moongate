using Moongate.Core.Types.Geometry;
using Moongate.Core.Utils;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Core.Utils;

public sealed class EnumNameUtilsTests
{
    [Fact]
    public void Format_APlainEnum_WritesTheSnakeCaseName()
    {
        Assert.Equal("game_master", EnumNameUtils.Format(AccountType.GameMaster));
        Assert.Equal("ter_mur", EnumNameUtils.Format(MapType.TerMur));
    }

    [Fact]
    public void Format_AFlagsValueWithItsOwnName_WritesThatName()
    {
        Assert.Equal("standalone", EnumNameUtils.Format(ServerMode.Standalone));
    }

    [Fact]
    public void Format_AFlagsCombination_JoinsTheNamesWithAPipe()
    {
        var text = EnumNameUtils.Format(TileFlagType.Impassable | TileFlagType.Surface);

        Assert.Equal(["impassable", "surface"], text.Split('|').Order());
    }

    [Fact]
    public void Format_ADirectionWhileRunning_KeepsTheDirectionWhole()
    {
        var text = EnumNameUtils.Format(DirectionType.SouthEast | DirectionType.Running);

        Assert.Equal(["running", "south_east"], text.Split('|').Order());
    }

    [Fact]
    public void Format_ZeroFlags_UsesTheZeroNameOrAnEmptyString()
    {
        Assert.Equal("none", EnumNameUtils.Format(TileFlagType.None));
        Assert.Equal(string.Empty, EnumNameUtils.Format(default(MovementAbilityType)));
    }

    [Fact]
    public void Format_AValueWithoutAName_Throws()
    {
        Assert.Throws<ArgumentException>(() => EnumNameUtils.Format((AccountType)42));
    }

    [Theory, InlineData("game_master"), InlineData("GameMaster"), InlineData("gamemaster"), InlineData(" game_master ")]
    public void TryParse_ANameInAnyCaseWithOrWithoutUnderscores_ReadsIt(string text)
    {
        Assert.True(EnumNameUtils.TryParse<AccountType>(text, out var value));
        Assert.Equal(AccountType.GameMaster, value);
    }

    [Theory, InlineData("impassable|surface"), InlineData("Surface | Impassable")]
    public void TryParse_PipeSeparatedFlags_CombinesThem(string text)
    {
        Assert.True(EnumNameUtils.TryParse<TileFlagType>(text, out var value));
        Assert.Equal(TileFlagType.Impassable | TileFlagType.Surface, value);
    }

    [Fact]
    public void TryParse_LoginAndGame_IsStandalone()
    {
        Assert.True(EnumNameUtils.TryParse<ServerMode>("login|game", out var value));
        Assert.Equal(ServerMode.Standalone, value);
    }

    [Fact]
    public void TryParse_AnEmptyStringForFlags_IsZero()
    {
        Assert.True(EnumNameUtils.TryParse<TileFlagType>("", out var value));
        Assert.Equal(TileFlagType.None, value);
    }

    [Theory, InlineData("1"), InlineData("99"), InlineData("admin"), InlineData("regular|game_master"), InlineData(""), InlineData(null)]
    public void TryParse_ANumberAnUnknownNameAPipeOnAPlainEnumOrNothing_ReturnsFalse(string? text)
    {
        Assert.False(EnumNameUtils.TryParse<AccountType>(text, out _));
    }

    [Theory, InlineData("impassable|"), InlineData("impassable|64"), InlineData("impassable|nope")]
    public void TryParse_AFlagsListWithABadPart_ReturnsFalse(string text)
    {
        Assert.False(EnumNameUtils.TryParse<TileFlagType>(text, out _));
    }

    [Fact]
    public void FormatThenTryParse_RoundTripsEveryDirectionWithAndWithoutRunning()
    {
        foreach (var direction in Enum.GetValues<DirectionType>().Where(direction => direction != DirectionType.Running))
        {
            foreach (var value in new[] { direction, direction | DirectionType.Running })
            {
                Assert.True(EnumNameUtils.TryParse<DirectionType>(EnumNameUtils.Format(value), out var back));
                Assert.Equal(value, back);
            }
        }
    }

    [Theory, InlineData("mountn_a"), InlineData("Mountn_a"), InlineData("MOUNTNA")]
    public void TryParse_AMemberNameWithItsOwnUnderscore_ReadsIt(string text)
    {
        Assert.True(EnumNameUtils.TryParse<MusicType>(text, out var value));
        Assert.Equal(MusicType.Mountn_a, value);
    }
}
