using Moongate.Core.Random;
using Moongate.Core.Utils;
using Moongate.Tests.TestSupport.Random;

namespace Moongate.Tests.Core.Utils;

[Collection("Global random state")]
public sealed class RandomUtilsTests
{
    [Theory, InlineData(false), InlineData(true)]
    public void CoinFlips_FullChunkCannotCountMoreThan62Heads(bool capped)
    {
        using var random = new RandomStateScope();

        // This MizuchiRandom state produces ulong.MaxValue on its next draw.
        // It exercises the upper two bits that are outside a 62-coin chunk.
        BuiltInRng.Generator.SetSelectedState(0, 0xC3499ADF7D5792F4UL);
        BuiltInRng.Generator.SetSelectedState(1, 1UL);

        var heads = capped ? RandomUtils.CoinFlips(62, 100) : RandomUtils.CoinFlips(62);

        Assert.Equal(62, heads);
    }

    [Theory, InlineData(0), InlineData(-1)]
    public void CoinFlips_NoCoinsReturnsZero(int amount)
    {
        Assert.Equal(0, RandomUtils.CoinFlips(amount));
        Assert.Equal(0, RandomUtils.CoinFlips(amount, 10));
    }

    [Theory, InlineData(1), InlineData(61), InlineData(62), InlineData(63), InlineData(125)]
    public void CoinFlips_StaysWithinRequestedCountAndCap(int amount)
    {
        using var random = new RandomStateScope();

        Assert.InRange(RandomUtils.CoinFlips(amount), 0, amount);
        Assert.InRange(RandomUtils.CoinFlips(amount, 3), 0, Math.Min(amount, 3));
        Assert.Equal(0, RandomUtils.CoinFlips(amount, 0));
        Assert.InRange(RandomUtils.CoinFlips(amount, amount + 1), 0, amount);
    }

    [Theory, InlineData(2), InlineData(6)]
    public void Dice_EachDieStartsAtOneAndBonusIsApplied(int sides)
    {
        using var random = new RandomStateScope();

        Assert.InRange(RandomUtils.Dice(20, sides, -3), 17, 20 * sides - 3);
    }

    [Theory, InlineData(0, 6), InlineData(-2, 6), InlineData(3, 0), InlineData(3, -1)]
    public void Dice_InvalidCountOrSidesReturnsZeroWithoutBonus(int amount, int sides)
        => Assert.Equal(0, RandomUtils.Dice(amount, sides, 100));

    [Fact]
    public void Dice_OneSidedDiceApplyCountAndBonus()
    {
        using var random = new RandomStateScope();

        Assert.Equal(12, RandomUtils.Dice(7, 1, 5));
    }

    [Fact]
    public void RandomBytes_WritesOnlyTheRequestedSliceAndSupportsAnEmptySpan()
    {
        using var random = new RandomStateScope();
        byte[] bytes = [11, 22, 33, 44, 55, 66];

        RandomUtils.RandomBytes(bytes.AsSpan(1, 4));
        RandomUtils.RandomBytes(Span<byte>.Empty);

        Assert.Equal(11, bytes[0]);
        Assert.Equal(66, bytes[^1]);
        Assert.NotEqual(new byte[] { 22, 33, 44, 55 }, bytes[1..5]);
    }

    [Fact]
    public void RandomDouble_ReturnsValueBelowOne()
    {
        using var random = new RandomStateScope();

        Assert.InRange(RandomUtils.RandomDouble(), 0, Math.BitDecrement(1.0));
    }

    [Fact]
    public void RandomList_HandlesAbsentAndSingletonInputsAndReturnsExistingMember()
    {
        using var random = new RandomStateScope();
        var instance = new object();
        var choices = new[] { new object(), new object(), new object() };

        Assert.Null(RandomUtils.RandomList<object>(null!));
        Assert.Null(RandomUtils.RandomList<object>());
        Assert.Equal(0, RandomUtils.RandomList<int>());
        Assert.Same(instance, RandomUtils.RandomList(instance));
        Assert.Contains(RandomUtils.RandomList(choices), choices);
    }

    [Theory, InlineData(4, 0, 3), InlineData(-4, -3, 0), InlineData(0, 0, 0)]
    public void Random_SignedCountMirrorsTheRange(int count, int lower, int upper)
    {
        using var random = new RandomStateScope();

        for (var i = 0; i < 16; i++)
        {
            Assert.InRange(RandomUtils.Random(count), lower, upper);
            Assert.InRange(RandomUtils.Random((long)count), lower, upper);
        }
    }

    [Theory, InlineData(5, 4, 5, 8), InlineData(-5, 3, -5, -3), InlineData(12, 0, 12, 12)]
    public void Random_UsesStartAndCountForIntegerAndLongRanges(int start, int count, int lower, int upper)
    {
        using var random = new RandomStateScope();

        for (var i = 0; i < 16; i++)
        {
            Assert.InRange(RandomUtils.Random(start, count), lower, upper);
            Assert.InRange(RandomUtils.Random((long)start, count), lower, upper);
        }
    }
}
