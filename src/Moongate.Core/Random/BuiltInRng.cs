using System.Runtime.CompilerServices;
using ShaiRandom.Generators;

namespace Moongate.Core.Random;

public static class BuiltInRng
{
    public static IEnhancedRandom Generator { get; private set; } = new MizuchiRandom();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Next()
    {
        return Generator.NextInt();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Next(int maxValue)
    {
        return Generator.NextInt(maxValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Next(int minValue, int count)
    {
        return minValue + Generator.NextInt(count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Next(long maxValue)
    {
        return Generator.NextLong(maxValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Next(long minValue, long count)
    {
        return minValue + Generator.NextLong(count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NextBytes(Span<byte> buffer)
    {
        Generator.NextBytes(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double NextDouble()
    {
        return Generator.NextDouble();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NextLong()
    {
        return Generator.NextLong();
    }

    public static void Reset()
    {
        Generator = new MizuchiRandom();
    }
}
