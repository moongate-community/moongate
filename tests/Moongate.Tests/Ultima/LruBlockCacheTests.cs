using Moongate.Ultima.Caching;

namespace Moongate.Tests.Ultima;

/// <summary>
/// The bound on map-block memory. What matters is that eviction happens and that a reader holding an
/// evicted block is unharmed — the arrays this replaced grew for the life of the process.
/// </summary>
public class LruBlockCacheTests
{
    [Fact]
    public void GetOrAdd_ReadsOnlyOnceForTheSameBlock()
    {
        var cache = new LruBlockCache<int[]>(8);
        var reads = 0;

        cache.GetOrAdd(LruBlockCache<int[]>.Key(1, 2), () => { reads++; return [1]; });
        cache.GetOrAdd(LruBlockCache<int[]>.Key(1, 2), () => { reads++; return [1]; });

        Assert.Equal(1, reads);
    }

    // The coordinates are packed into one long; two different blocks must not collide, and the pack
    // has to survive a y that would sign-extend if it were not masked.
    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(1, 0, 0, 1)]
    [InlineData(767, 511, 767, 512)]
    public void Key_DistinctBlocksGetDistinctKeys(int x1, int y1, int x2, int y2)
        => Assert.NotEqual(LruBlockCache<int[]>.Key(x1, y1), LruBlockCache<int[]>.Key(x2, y2));

    [Fact]
    public void Key_TheSameBlockAlwaysPacksTheSame()
        => Assert.Equal(LruBlockCache<int[]>.Key(300, 400), LruBlockCache<int[]>.Key(300, 400));

    [Fact]
    public void GetOrAdd_PastCapacity_EvictsAndStaysAtTheCap()
    {
        var cache = new LruBlockCache<int[]>(4);

        for (var block = 0; block < 20; block++)
        {
            cache.GetOrAdd(LruBlockCache<int[]>.Key(block, 0), () => [block]);
        }

        Assert.Equal(4, cache.Count);
        Assert.Equal(16, cache.EvictedCount);
    }

    // Least-RECENTLY-used, not least-recently-added: touching a block has to save it.
    [Fact]
    public void GetOrAdd_TouchingABlock_SpareItFromEviction()
    {
        var cache = new LruBlockCache<int[]>(2);
        var oldest = LruBlockCache<int[]>.Key(0, 0);

        cache.GetOrAdd(oldest, () => [0]);
        cache.GetOrAdd(LruBlockCache<int[]>.Key(1, 0), () => [1]);
        cache.GetOrAdd(oldest, () => [99]);
        cache.GetOrAdd(LruBlockCache<int[]>.Key(2, 0), () => [2]);

        Assert.True(cache.TryGet(oldest, out _));
        Assert.False(cache.TryGet(LruBlockCache<int[]>.Key(1, 0), out _));
    }

    /// <summary>
    /// A block is a plain array, so a caller holding one that gets evicted keeps reading it. This is
    /// what makes evicting map blocks safe where evicting bitmaps was not.
    /// </summary>
    [Fact]
    public void AnEvictedBlock_IsStillUsableByWhoeverHeldIt()
    {
        var cache = new LruBlockCache<int[]>(1);
        var held = cache.GetOrAdd(LruBlockCache<int[]>.Key(0, 0), () => [7, 8, 9]);

        cache.GetOrAdd(LruBlockCache<int[]>.Key(1, 0), () => [0]);

        Assert.False(cache.TryGet(LruBlockCache<int[]>.Key(0, 0), out _));
        Assert.Equal([7, 8, 9], held);
    }

    [Fact]
    public void SetCapacity_Lowered_GivesTheMemoryBackAtOnce()
    {
        var cache = new LruBlockCache<int[]>(10);

        for (var block = 0; block < 10; block++)
        {
            cache.GetOrAdd(LruBlockCache<int[]>.Key(block, 0), () => [block]);
        }

        cache.SetCapacity(3);

        Assert.Equal(3, cache.Count);
    }

    [Fact]
    public void Set_ReplacesABlockWithoutGrowingTheCache()
    {
        var cache = new LruBlockCache<int[]>(4);
        var key = LruBlockCache<int[]>.Key(5, 5);

        cache.GetOrAdd(key, () => [1]);
        cache.Set(key, [2]);

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet(key, out var value));
        Assert.Equal([2], value);
    }

    // Capacity 0 means "do not cache", not "cache everything": the read still has to answer.
    [Fact]
    public void GetOrAdd_WithCapacityZero_StillReadsAndReturns()
    {
        var cache = new LruBlockCache<int[]>(0);

        Assert.Equal([4], cache.GetOrAdd(LruBlockCache<int[]>.Key(0, 0), () => [4]));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void ANegativeCapacity_IsRefused()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new LruBlockCache<int[]>(-1));

    /// <summary>
    /// The arrays this replaced were filled with an unsynchronized read-modify-write, read from the
    /// game loop and from the HTTP threads serving map tiles at the same time. Concurrent readers of
    /// one block must see one read and one array.
    /// </summary>
    [Fact]
    public void ConcurrentReadersOfOneBlock_ReadItOnceAndAllSeeTheSameArray()
    {
        var cache = new LruBlockCache<int[]>(64);
        var reads = 0;
        var key = LruBlockCache<int[]>.Key(3, 4);

        var results = new int[64][];

        Parallel.For(
            0,
            results.Length,
            index => results[index] = cache.GetOrAdd(key, () => { Interlocked.Increment(ref reads); return [1]; })
        );

        Assert.Equal(1, reads);
        Assert.All(results, array => Assert.Same(results[0], array));
    }
}
