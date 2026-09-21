using System.Buffers;
using Moongate.Core.Collections;

namespace Moongate.Tests.Core.Collections;

public class PooledRefListTests
{
    [Theory, InlineData(false), InlineData(true)]
    public void Add_GrowsAndTrimsInBothModes_PreservesItems(bool mt)
    {
        var list = new PooledRefList<int>(1, mt);

        try
        {
            for (var i = 0; i < 40; i++)
            {
                list.Add(i);
            }

            list.TrimExcess();
            Assert.Equal(40, list.Count);
            Assert.Equal(Enumerable.Range(0, 40), list.ToArray());
            var array = list.ToPooledArray();

            try
            {
                Assert.Equal(Enumerable.Range(0, 40), array.AsSpan(0, list.Count).ToArray());
                array[0] = -99;
                Assert.Equal(0, list[0]);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(array);
            }

            list.Clear();
            list.Capacity = 0;
            list.Add(99);
            Assert.Equal(99, list[0]);
        }
        finally
        {
            list.Dispose();
            list.Dispose();
        }
    }

    [Fact]
    public void BinarySearch_CustomComparer_UsesRequestedOrdering()
    {
        using var list = new PooledRefList<string>(new[] { "ALPHA", "beta", "Gamma" });

        Assert.Equal(1, list.BinarySearch("BETA", StringComparer.OrdinalIgnoreCase));
        Assert.Equal(-3, list.BinarySearch("delta", StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void BinarySearch_FindsItemsAndReportsInsertionPositionsWithinRange()
    {
        using var list = new PooledRefList<int>(new[] { 2, 4, 6, 8, 10 });

        Assert.Equal(2, list.BinarySearch(6));
        Assert.Equal(-4, list.BinarySearch(7));
        Assert.Equal(-2, list.BinarySearch(1, 3, 2, null));
        Assert.Equal(-5, list.BinarySearch(1, 3, 10, null));
        Assert.Equal(-6, list.BinarySearch(5, 0, 12, null));
    }

    [Theory, InlineData(-1, 1, "index"), InlineData(0, -1, "count")]
    public void BinarySearch_NegativeRange_Throws(int index, int count, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.BinarySearch(index, count, 20, null);
            }
        );

        Assert.Equal(parameter, exception.ParamName);
    }

    [Theory, InlineData(2, 2), InlineData(4, 0)]
    public void BinarySearch_RangeBeyondCount_Throws(int index, int count)
        => Assert.Throws<ArgumentException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.BinarySearch(index, count, 20, null);
            }
        );

    [Fact]
    public void Capacity_ShrinkBelowCount_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                var list = new PooledRefList<int>(new[] { 4, 8 });

                try
                {
                    list.Capacity = 1;
                }
                finally
                {
                    var contents = list.ToArray();
                    list.Dispose();
                    Assert.Equal(new[] { 4, 8 }, contents);
                }
            }
        );

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Clear_ReleasesStoredReferencesAndAllowsReuse()
    {
        using var list = new PooledRefList<string>(new[] { "first", "last" });
        var borrowed = list.AsSpan();

        list.Clear();

        Assert.Null(borrowed[0]);
        Assert.Null(borrowed[1]);
        Assert.Empty(list.ToArray());
        list.Clear();
        list.Add("fresh");
        Assert.Equal(new[] { "fresh" }, list.ToArray());
    }

    [Fact]
    public void Constructor_NegativeCapacity_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(-1);
            }
        );

        Assert.Equal("capacity", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullCollection_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var list = new PooledRefList<int>(null!);
            }
        );

        Assert.Equal("collection", exception.ParamName);
    }

    [Theory, InlineData(false), InlineData(true)]
    public void Constructors_CopyCollections_KeepIndependentStorage(bool mt)
    {
        var source = new PooledRefList<string>(new[] { "first", "second" }, mt);

        try
        {
            using var copy = new PooledRefList<string>(source, mt);
            using var iteratorCopy = new PooledRefList<string>(Enumerable.Range(1, 2).Select(i => i.ToString()), mt);
            source[0] = "changed";

            Assert.Equal(new[] { "first", "second" }, copy.ToArray());
            Assert.Equal(new[] { "1", "2" }, iteratorCopy.ToArray());
        }
        finally
        {
            source.Dispose();
        }
    }

    [Fact]
    public void ConvertAll_ConvertsOnlyLiveItemsInOrder()
    {
        using var list = new PooledRefList<int>(new[] { 2, 4, 6 });
        var visited = new List<int>();
        using var converted = list.ConvertAll(
            value =>
            {
                visited.Add(value);

                return value switch
                {
                    2 => "two",
                    4 => "four",
                    _ => "six"
                };
            }
        );

        Assert.Equal(new[] { 2, 4, 6 }, visited);
        Assert.Equal(new[] { "two", "four", "six" }, converted.ToArray());
    }

    [Fact]
    public void ConvertAll_NullCallback_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var list = new PooledRefList<int>(0);
                list.ConvertAll<string>(null!);
            }
        );

        Assert.Equal("converter", exception.ParamName);
    }

    [Fact]
    public void CopyTo_RangeBeyondCount_ThrowsWithoutWritingDestination()
    {
        var destination = new[] { -1, -1, -1 };

        Assert.Throws<ArgumentException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20 });
                list.CopyTo(1, destination, 0, 2);
            }
        );

        Assert.Equal(new[] { -1, -1, -1 }, destination);
    }

    [Fact]
    public void CopyTo_WholeListAndRanges_PreserveDestinationOutsideCopiedItems()
    {
        using var list = new PooledRefList<int>(new[] { 10, 20, 30, 40 });
        int[] whole = [-1, -1, -1, -1, -1];
        int[] offset = [-1, -1, -1, -1, -1, -1];
        int[] range = [-1, -1, -1, -1, -1];

        list.CopyTo(whole);
        list.CopyTo(offset, 1);
        list.CopyTo(1, range, 2, 2);

        Assert.Equal(new[] { 10, 20, 30, 40, -1 }, whole);
        Assert.Equal(new[] { -1, 10, 20, 30, 40, -1 }, offset);
        Assert.Equal(new[] { -1, -1, 20, 30, -1 }, range);
    }

    [Fact]
    public void EmptyConstructors_ProduceListsThatCanBePopulatedIndependently()
    {
        using var source = new PooledRefList<int>(0);
        using var copy = new PooledRefList<int>(source);
        using var collectionCopy = new PooledRefList<int>(Array.Empty<int>());
        using var iteratorCopy = new PooledRefList<int>(Enumerable.Empty<int>().Where(_ => true));

        Assert.Empty(source.ToArray());
        Assert.Empty(source.ToPooledArray());
        copy.Add(4);
        collectionCopy.Add(8);
        iteratorCopy.Add(12);

        Assert.Empty(source.ToArray());
        Assert.Equal(new[] { 4 }, copy.ToArray());
        Assert.Equal(new[] { 8 }, collectionCopy.ToArray());
        Assert.Equal(new[] { 12 }, iteratorCopy.ToArray());
    }

    [Fact]
    public void EnsureCapacity_GrowsPopulatedListAndPreservesContents()
    {
        using var list = PooledRefList<string>.Create(0);
        list.Add("first");
        list.Add("last");
        var requested = list.Capacity + 1;

        Assert.True(list.EnsureCapacity(requested) >= requested);
        var capacity = list.Capacity;
        Assert.Equal(capacity, list.EnsureCapacity(0));
        Assert.Equal(new[] { "first", "last" }, list.ToArray());
    }

    [Fact]
    public void EnsureCapacity_NegativeCapacity_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(0);
                list.EnsureCapacity(-1);
            }
        );

        Assert.Equal("capacity", exception.ParamName);
    }

    [Theory, InlineData(false), InlineData(true)]
    public void Enumerator_CurrentBeforeStartOrAfterEnd_Throws(bool afterEnd)
        => Assert.Throws<InvalidOperationException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 4 });
                var enumerator = list.GetEnumerator();

                try
                {
                    if (afterEnd)
                    {
                        Assert.True(enumerator.MoveNext());
                        Assert.False(enumerator.MoveNext());
                    }

                    _ = enumerator.Current;
                }
                finally
                {
                    enumerator.Dispose();
                }
            }
        );

    [Theory, InlineData(false), InlineData(true)]
    public void Enumerator_Reset_RestartsAtFirstItem(bool afterEnd)
    {
        using var list = new PooledRefList<int>(new[] { 4, 8 });
        var enumerator = list.GetEnumerator();

        try
        {
            Assert.True(enumerator.MoveNext());

            if (afterEnd)
            {
                Assert.True(enumerator.MoveNext());
                Assert.False(enumerator.MoveNext());
            }

            enumerator.Reset();

            Assert.True(enumerator.MoveNext());
            Assert.Equal(4, enumerator.Current);
        }
        finally
        {
            enumerator.Dispose();
        }
    }

    [Fact]
    public void Enumerator_VisitsLiveItemsAndStaysEnded()
    {
        using var list = new PooledRefList<int>(new[] { 4, 8, 12 });
        var visited = new List<int>();
        var enumerator = list.GetEnumerator();

        try
        {
            while (enumerator.MoveNext())
            {
                visited.Add(enumerator.Current);
            }

            Assert.False(enumerator.MoveNext());
            Assert.Equal(new[] { 4, 8, 12 }, visited);
        }
        finally
        {
            enumerator.Dispose();
        }
    }

    [Fact]
    public void FindAll_MatchingItems_PreservesOrderAndOwnsItsStorage()
    {
        var source = new PooledRefList<int>(new[] { 1, 4, 6, 3, 8 });

        try
        {
            using var matches = source.FindAll(value => value % 2 == 0);
            source[1] = 99;

            Assert.Equal(new[] { 4, 6, 8 }, matches.ToArray());
        }
        finally
        {
            source.Dispose();
        }
    }

    [Fact]
    public void FindAll_NoMatches_ReturnsAnEmptyListThatCanBePopulated()
    {
        using var source = new PooledRefList<int>(new[] { 1, 3 });
        using var matches = source.FindAll(value => value % 2 == 0);

        Assert.Empty(matches.ToArray());
        matches.Add(4);

        Assert.Equal(new[] { 4 }, matches.ToArray());
        Assert.Equal(new[] { 1, 3 }, source.ToArray());
    }

    [Fact]
    public void FindAll_NullCallback_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var list = new PooledRefList<int>(0);
                list.FindAll(null!);
            }
        );

        Assert.Equal("match", exception.ParamName);
    }

    [Fact]
    public void FindAll_PredicateThrowsAfterMatch_PreservesExceptionAndReusableSource()
    {
        using var source = new PooledRefList<int>(new[] { 1, 2, 3 });
        var expected = new InvalidOperationException("Predicate failed.");
        var visited = new List<int>();
        Exception? actual = null;

        try
        {
            using var matches = source.FindAll(
                value =>
                {
                    visited.Add(value);

                    return value == 1 ? true : throw expected;
                }
            );
        }
        catch (Exception caught)
        {
            actual = caught;
        }

        Assert.Same(expected, actual);
        Assert.Equal(new[] { 1, 2 }, visited);
        Assert.Equal(new[] { 1, 2, 3 }, source.ToArray());

        source.Add(4);
        using var retry = source.FindAll(value => value % 2 == 0);

        Assert.Equal(new[] { 1, 2, 3, 4 }, source.ToArray());
        Assert.Equal(new[] { 2, 4 }, retry.ToArray());
    }

    [Theory,
     InlineData(-1, 1, "startIndex"),
     InlineData(4, 0, "startIndex"),
     InlineData(1, -1, "count"),
     InlineData(1, 3, "count")]
    public void FindIndex_InvalidRange_Throws(int startIndex, int count, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.FindIndex(startIndex, count, _ => true);
            }
        );

        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void FindIndex_NullCallback_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var list = new PooledRefList<int>(0);
                list.FindIndex(null!);
            }
        );

        Assert.Equal("match", exception.ParamName);
    }

    [Fact]
    public void FindLastIndex_EmptyListWithNonnegativeStartIndex_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(0);
                list.FindLastIndex(0, 0, _ => true);
            }
        );

        Assert.Equal("startIndex", exception.ParamName);
    }

    [Theory,
     InlineData(-1, 1, "startIndex"),
     InlineData(3, 0, "startIndex"),
     InlineData(1, -1, "count"),
     InlineData(1, 3, "count")]
    public void FindLastIndex_InvalidRange_Throws(int startIndex, int count, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.FindLastIndex(startIndex, count, _ => true);
            }
        );

        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void FindLastIndex_NullCallback_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var list = new PooledRefList<int>(0);
                list.FindLastIndex(null!);
            }
        );

        Assert.Equal("match", exception.ParamName);
    }

    [Fact]
    public void FindLast_NullCallback_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var list = new PooledRefList<int>(0);
                list.FindLast(null!);
            }
        );

        Assert.Equal("match", exception.ParamName);
    }

    [Fact]
    public void FindPredicates_SelectFirstAndLastMatches_AndReturnDefaultWhenMissing()
    {
        using var list = new PooledRefList<int>(new[] { 1, 4, 6, 3, 8 });

        Assert.True(list.Exists(value => value == 6));
        Assert.False(list.Exists(value => value == 9));
        Assert.Equal(4, list.Find(value => value % 2 == 0));
        Assert.Equal(8, list.FindLast(value => value % 2 == 0));
        Assert.Equal(0, list.Find(value => value > 10));
        Assert.Equal(0, list.FindLast(value => value > 10));
        Assert.Equal(1, list.FindIndex(value => value % 2 == 0));
        Assert.Equal(2, list.FindIndex(2, value => value % 2 == 0));
        Assert.Equal(-1, list.FindIndex(3, 1, value => value % 2 == 0));
        Assert.Equal(4, list.FindLastIndex(value => value % 2 == 0));
        Assert.Equal(2, list.FindLastIndex(3, value => value % 2 == 0));
        Assert.Equal(-1, list.FindLastIndex(3, 1, value => value % 2 == 0));
        Assert.True(list.TrueForAll(value => value > 0));
        Assert.False(list.TrueForAll(value => value < 8));
    }

    [Fact]
    public void Find_NullCallback_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var list = new PooledRefList<int>(0);
                list.Find(null!);
            }
        );

        Assert.Equal("match", exception.ParamName);
    }

    [Fact]
    public void ForEach_InvokesActionForLiveItemsInOrder()
    {
        using var list = new PooledRefList<int>(new[] { 3, 1, 4 });
        var visited = new List<int>();

        list.ForEach(visited.Add);
        list.Clear();
        list.ForEach(_ => throw new InvalidOperationException("Empty list must not invoke the callback."));

        Assert.Equal(new[] { 3, 1, 4 }, visited);
    }

    [Fact]
    public void ForEach_NullCallback_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var list = new PooledRefList<int>(0);
                list.ForEach(null!);
            }
        );

        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public void GetRange_CopiesSelectedItemsIntoIndependentList()
    {
        var list = new PooledRefList<int>(new[] { 10, 20, 30, 40 });

        try
        {
            using var range = list.GetRange(1, 2);
            using var empty = list.GetRange(4, 0);
            list[1] = 99;
            range.Add(50);

            Assert.Equal(new[] { 20, 30, 50 }, range.ToArray());
            Assert.Empty(empty.ToArray());
            Assert.Equal(new[] { 10, 99, 30, 40 }, list.ToArray());
        }
        finally
        {
            list.Dispose();
        }
    }

    [Theory, InlineData(-1, 1, "index"), InlineData(0, -1, "count")]
    public void GetRange_NegativeRange_Throws(int index, int count, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.GetRange(index, count);
            }
        );

        Assert.Equal(parameter, exception.ParamName);
    }

    [Theory, InlineData(2, 2), InlineData(4, 0)]
    public void GetRange_RangeBeyondCount_Throws(int index, int count)
        => Assert.Throws<ArgumentException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.GetRange(index, count);
            }
        );

    [Theory, InlineData(4, 0, "index"), InlineData(1, -1, "count"), InlineData(1, 3, "count")]
    public void IndexOf_InvalidRange_Throws(int index, int count, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.IndexOf(20, index, count);
            }
        );

        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void IndexOf_StartBeyondCount_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.IndexOf(20, 4);
            }
        );

        Assert.Equal("index", exception.ParamName);
    }

    [Theory, InlineData(-1), InlineData(2)]
    public void IndexerGet_OutsideCount_Throws(int index)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 4, 8 });
                _ = list[index];
            }
        );

        Assert.Equal("index", exception.ParamName);
    }

    [Theory, InlineData(-1), InlineData(2)]
    public void IndexerSet_OutsideCount_ThrowsWithoutChangingItems(int index)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                var list = new PooledRefList<int>(new[] { 4, 8 });

                try
                {
                    list[index] = 99;
                }
                finally
                {
                    var contents = list.ToArray();
                    list.Dispose();
                    Assert.Equal(new[] { 4, 8 }, contents);
                }
            }
        );

        Assert.Equal("index", exception.ParamName);
    }

    [Fact]
    public void InsertRange_CollectionsAndIterators_PreserveInsertedAndExistingOrder()
    {
        using var list = new PooledRefList<int>(new[] { 10, 40 });

        list.InsertRange(1, new[] { 20, 30 });
        list.InsertRange(2, new[] { 21, 22 }.Where(_ => true));
        list.InsertRange(list.Count, Array.Empty<int>());
        list.InsertRange(0, Enumerable.Empty<int>().Where(_ => true));
        list.InsertRange(list.Count, new[] { 50 });

        Assert.Equal(new[] { 10, 20, 21, 22, 30, 40, 50 }, list.ToArray());
    }

    [Fact]
    public void InsertRange_ExceedingCapacity_GrowsAndPreservesBothSidesOfInsertion()
    {
        using var list = new PooledRefList<int>(new[] { -1, -2 });
        var inserted = Enumerable.Range(1, list.Capacity * 2 + 1).ToArray();

        list.InsertRange(1, inserted);

        Assert.Equal(new[] { -1 }.Concat(inserted).Concat(new[] { -2 }), list.ToArray());
    }

    [Theory, InlineData(-1), InlineData(3)]
    public void InsertRange_InvalidIndex_ThrowsWithoutChangingItems(int index)
        => Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20 });

                try
                {
                    list.InsertRange(index, new[] { 99 });
                }
                finally
                {
                    Assert.Equal(new[] { 10, 20 }, list.ToArray());
                }
            }
        );

    [Fact]
    public void InsertRange_NullCollection_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var list = new PooledRefList<int>(0);
                list.InsertRange(0, null!);
            }
        );

        Assert.Equal("collection", exception.ParamName);
    }

    [Fact]
    public void Insert_AtStartMiddleAndEnd_ShiftsItemsInOrder()
    {
        using var list = PooledRefList<int>.CreateMT(0);

        list.Insert(0, 20);
        list.Insert(0, 10);
        list.Insert(1, 15);
        list.Insert(list.Count, 30);

        Assert.Equal(new[] { 10, 15, 20, 30 }, list.ToArray());
    }

    [Theory, InlineData(-1), InlineData(3)]
    public void Insert_InvalidIndex_ThrowsWithoutChangingItems(int index)
        => Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20 });

                try
                {
                    list.Insert(index, 99);
                }
                finally
                {
                    Assert.Equal(new[] { 10, 20 }, list.ToArray());
                }
            }
        );

    [Theory, InlineData(-1, 1, "index"), InlineData(3, 0, "index"), InlineData(1, -1, "count"), InlineData(1, 3, "count")]
    public void LastIndexOf_InvalidRange_Throws(int index, int count, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.LastIndexOf(20, index, count);
            }
        );

        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void LastIndexOf_StartAtCount_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.LastIndexOf(20, 3);
            }
        );

        Assert.Equal("index", exception.ParamName);
    }

    [Fact]
    public void RemoveAll_CompactsMixedMatchesAndSupportsNoMatchesAndAllMatches()
    {
        using var list = new PooledRefList<string>(new[] { "keep-a", "drop", "drop", "keep-b", "drop" });

        Assert.Equal(3, list.RemoveAll(value => value == "drop"));
        Assert.Equal(new[] { "keep-a", "keep-b" }, list.ToArray());
        Assert.Equal(0, list.RemoveAll(value => value == "missing"));
        Assert.Equal(2, list.RemoveAll(_ => true));
        Assert.Empty(list.ToArray());
        list.Add("fresh");
        Assert.Equal(new[] { "fresh" }, list.ToArray());
    }

    [Fact]
    public void RemoveAll_NullCallback_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var list = new PooledRefList<int>(0);
                list.RemoveAll(null!);
            }
        );

        Assert.Equal("match", exception.ParamName);
    }

    [Fact]
    public void RemoveAll_ReleasesReferencesFromEveryVacatedSlot()
    {
        using var list = new PooledRefList<string>(new[] { "drop", "keep", "drop" });
        var borrowed = list.AsSpan();

        Assert.Equal(2, list.RemoveAll(value => value == "drop"));

        Assert.Equal(new[] { "keep" }, list.ToArray());
        Assert.Null(borrowed[1]);
        Assert.Null(borrowed[2]);
    }

    [Theory, InlineData(-1), InlineData(2)]
    public void RemoveAt_InvalidIndex_ThrowsWithoutChangingItems(int index)
        => Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20 });

                try
                {
                    list.RemoveAt(index);
                }
                finally
                {
                    Assert.Equal(new[] { 10, 20 }, list.ToArray());
                }
            }
        );

    [Fact]
    public void RemoveAt_ReleasesReferenceFromVacatedLastSlot()
    {
        using var list = new PooledRefList<string>(new[] { "first", "middle", "last" });
        var borrowed = list.AsSpan();

        list.RemoveAt(1);

        Assert.Equal(new[] { "first", "last" }, list.ToArray());
        Assert.Null(borrowed[2]);
    }

    [Theory, InlineData(-1, 1, "index"), InlineData(0, -1, "count")]
    public void RemoveRange_NegativeRange_Throws(int index, int count, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.RemoveRange(index, count);
            }
        );

        Assert.Equal(parameter, exception.ParamName);
    }

    [Theory, InlineData(2, 2), InlineData(4, 0)]
    public void RemoveRange_RangeBeyondCount_Throws(int index, int count)
        => Assert.Throws<ArgumentException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.RemoveRange(index, count);
            }
        );

    [Fact]
    public void RemoveRange_ReleasesReferencesFromEveryVacatedSlot()
    {
        using var list = new PooledRefList<string>(new[] { "first", "middle", "last" });
        var borrowed = list.AsSpan();

        list.RemoveRange(0, 2);

        Assert.Equal(new[] { "last" }, list.ToArray());
        Assert.Null(borrowed[1]);
        Assert.Null(borrowed[2]);
    }

    [Fact]
    public void RemoveRange_RemovesMiddleAndTail_AndAllowsEmptyRange()
    {
        using var list = new PooledRefList<string>(new[] { "a", "b", "c", "d", "e" });

        list.RemoveRange(1, 2);
        Assert.Equal(new[] { "a", "d", "e" }, list.ToArray());
        list.RemoveRange(2, 1);
        list.RemoveRange(2, 0);
        Assert.Equal(new[] { "a", "d" }, list.ToArray());
        list.RemoveRange(0, 2);
        Assert.Empty(list.ToArray());
    }

    [Fact]
    public void Remove_DeletesFirstMatchAndRetainsOrder()
    {
        using var list = new PooledRefList<string>(new[] { "first", "match", "last", "match" });

        Assert.True(list.Remove("match"));
        Assert.False(list.Remove("missing"));
        list.RemoveAt(list.Count - 1);

        Assert.Equal(new[] { "first", "last" }, list.ToArray());
    }

    [Theory, InlineData(-1, 1, "index"), InlineData(0, -1, "count")]
    public void Reverse_NegativeRange_Throws(int index, int count, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.Reverse(index, count);
            }
        );

        Assert.Equal(parameter, exception.ParamName);
    }

    [Theory, InlineData(2, 2), InlineData(4, 0)]
    public void Reverse_RangeBeyondCount_Throws(int index, int count)
        => Assert.Throws<ArgumentException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.Reverse(index, count);
            }
        );

    [Fact]
    public void Reverse_WholeListAndSubrange_LeaveOutsideItemsInPlace()
    {
        using var list = new PooledRefList<int>(new[] { 1, 2, 3, 4, 5 });

        list.Reverse(1, 3);
        Assert.Equal(new[] { 1, 4, 3, 2, 5 }, list.ToArray());
        list.Reverse(5, 0);
        list.Reverse(2, 1);
        list.Reverse();

        Assert.Equal(new[] { 5, 2, 3, 4, 1 }, list.ToArray());
    }

    [Fact]
    public void Search_DuplicatesAndMissingItems_RespectSelectedRange()
    {
        using var list = new PooledRefList<string?>(new[] { "a", null, "b", "a", "b" });

        Assert.True(list.Contains(null));
        Assert.False(list.Contains("missing"));
        Assert.Equal(0, list.IndexOf("a"));
        Assert.Equal(3, list.IndexOf("a", 1));
        Assert.Equal(-1, list.IndexOf("a", 1, 2));
        Assert.Equal(4, list.LastIndexOf("b"));
        Assert.Equal(2, list.LastIndexOf("b", 3));
        Assert.Equal(-1, list.LastIndexOf("a", 2, 2));
        Assert.Equal(-1, list.IndexOf("a", list.Count, 0));
        Assert.Equal(-1, list.LastIndexOf("a", 2, 0));
    }

    [Fact]
    public void Search_EmptyList_ReturnsNoMatch()
    {
        using var list = new PooledRefList<int>(0);

        Assert.False(list.Contains(0));
        Assert.Equal(-1, list.IndexOf(0));
        Assert.Equal(-1, list.LastIndexOf(0));
        Assert.Equal(-1, list.LastIndexOf(0, -1, 0));
        Assert.Equal(-1, list.FindIndex(_ => true));
        Assert.Equal(-1, list.FindLastIndex(_ => true));
        Assert.True(list.TrueForAll(_ => false));
    }

    [Fact]
    public void Sort_DefaultComparerCustomComparerAndComparison_RespectOrderingAndRange()
    {
        using var list = new PooledRefList<int>(new[] { 9, 3, 1, 2, 8 });

        list.Sort(1, 3, null);
        Assert.Equal(new[] { 9, 1, 2, 3, 8 }, list.ToArray());
        list.Sort();
        Assert.Equal(new[] { 1, 2, 3, 8, 9 }, list.ToArray());
        list.Sort(Comparer<int>.Create((left, right) => right.CompareTo(left)));
        Assert.Equal(new[] { 9, 8, 3, 2, 1 }, list.ToArray());
        list.Sort((left, right) => left.CompareTo(right));
        Assert.Equal(new[] { 1, 2, 3, 8, 9 }, list.ToArray());
    }

    [Theory, InlineData(-1, 1, "index"), InlineData(0, -1, "count")]
    public void Sort_NegativeRange_Throws(int index, int count, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.Sort(index, count, null);
            }
        );

        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void Sort_NullCallback_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var list = new PooledRefList<int>(0);
                list.Sort((Comparison<int>)null!);
            }
        );

        Assert.Equal("comparison", exception.ParamName);
    }

    [Theory, InlineData(2, 2), InlineData(4, 0)]
    public void Sort_RangeBeyondCount_Throws(int index, int count)
        => Assert.Throws<ArgumentException>(
            () =>
            {
                using var list = new PooledRefList<int>(new[] { 10, 20, 30 });
                list.Sort(index, count, null);
            }
        );

    [Theory, InlineData(0), InlineData(1)]
    public void Sort_ZeroOrOneItem_DoesNotInvokeComparison(int count)
    {
        using var list = new PooledRefList<int>(new[] { 7 }.Take(count));

        list.Sort();
        list.Sort(0, count, Comparer<int>.Create((_, _) => throw new InvalidOperationException()));
        list.Sort((_, _) => throw new InvalidOperationException());

        Assert.Equal(count == 0 ? Array.Empty<int>() : new[] { 7 }, list.ToArray());
    }

    [Fact]
    public void TrueForAll_NullCallback_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () =>
            {
                using var list = new PooledRefList<int>(0);
                list.TrueForAll(null!);
            }
        );

        Assert.Equal("match", exception.ParamName);
    }

    [Fact]
    public void ValueTypeRemoval_CompactsItemsWithoutIncludingUnusedCapacity()
    {
        using var list = new PooledRefList<int>(new[] { 1, 2, 3, 4, 5, 6 });

        list.RemoveAt(0);
        list.RemoveRange(1, 1);
        Assert.Equal(2, list.RemoveAll(value => value % 2 == 0 && value > 2));

        Assert.Equal(new[] { 2, 5 }, list.AsSpan().ToArray());
    }
}
