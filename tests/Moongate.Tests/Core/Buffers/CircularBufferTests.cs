using System.Collections;
using Moongate.Core.Buffers;

namespace Moongate.Tests.Core.Buffers;

public class CircularBufferTests
{
    [Theory, InlineData(0), InlineData(-1)]
    public void Constructor_NonpositiveCapacity_Throws(int capacity)
    {
        var exception = Assert.Throws<ArgumentException>(() => new CircularBuffer<int>(capacity));

        Assert.Equal("capacity", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullItems_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new CircularBuffer<int>(3, null!));

        Assert.Equal("items", exception.ParamName);
    }

    [Fact]
    public void Constructor_ItemsExceedCapacity_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => new CircularBuffer<int>(2, [1, 2, 3]));

        Assert.Equal("items", exception.ParamName);
    }

    [Fact]
    public void Constructor_InitialItems_CopiesInputAndAllowsFillingRemainingCapacity()
    {
        int[] input = [10, 20];
        var buffer = new CircularBuffer<int>(3, input);
        input[0] = 99;

        Assert.False(buffer.IsFull);
        AssertContents(buffer, 10, 20);

        buffer.PushBack(30);

        Assert.True(buffer.IsFull);
        AssertContents(buffer, 10, 20, 30);
    }

    [Fact]
    public void EmptyBuffer_EnumerationsAndSegments_AreEmpty()
    {
        var buffer = new CircularBuffer<int>(3);

        Assert.True(buffer.IsEmpty);
        Assert.False(buffer.IsFull);
        Assert.Equal(0, buffer.Size);
        Assert.Empty(buffer.ToArray());
        Assert.Empty(buffer);
        Assert.Empty(((IEnumerable)buffer).Cast<int>());
        Assert.Collection(buffer.ToArraySegments(), segment => Assert.Equal(0, segment.Count), segment => Assert.Equal(0, segment.Count));
    }

    [Fact]
    public void EmptyBuffer_EndpointAccessAndRemoval_Throw()
    {
        var buffer = new CircularBuffer<int>(3);

        Assert.Throws<InvalidOperationException>(() => buffer.Front());
        Assert.Throws<InvalidOperationException>(() => buffer.Back());
        Assert.Throws<InvalidOperationException>(() => buffer.PopFront());
        Assert.Throws<InvalidOperationException>(() => buffer.PopBack());
        Assert.Throws<IndexOutOfRangeException>(() => buffer[0]);
        Assert.Throws<IndexOutOfRangeException>(() => buffer[0] = 1);
        Assert.True(buffer.IsEmpty);
    }

    [Fact]
    public void PushBack_FullBuffer_OverwritesOldestItemsAndWraps()
    {
        var buffer = new CircularBuffer<int>(3, [1, 2, 3]);

        buffer.PushBack(4);
        buffer.PushBack(5);

        Assert.True(buffer.IsFull);
        AssertContents(buffer, 3, 4, 5);
    }

    [Fact]
    public void PushFront_FullBuffer_OverwritesLastItemsAndWraps()
    {
        var buffer = new CircularBuffer<int>(3, [1, 2, 3]);

        buffer.PushFront(0);
        buffer.PushFront(-1);

        Assert.True(buffer.IsFull);
        AssertContents(buffer, -1, 0, 1);
    }

    [Fact]
    public void PushAndPop_BothEnds_PreserveLogicalOrderAcrossWraparound()
    {
        var buffer = new CircularBuffer<int>(4);
        buffer.PushBack(1);
        buffer.PushBack(2);
        buffer.PushFront(0);
        AssertContents(buffer, 0, 1, 2);

        buffer.PopBack();
        buffer.PushBack(3);
        buffer.PushBack(4);
        Assert.True(buffer.IsFull);
        AssertContents(buffer, 0, 1, 3, 4);

        buffer.PopFront();
        buffer.PushFront(9);
        buffer.PopBack();
        buffer.PopBack();

        Assert.False(buffer.IsFull);
        AssertContents(buffer, 9, 1);
    }

    [Theory, InlineData(true), InlineData(false)]
    public void Pop_DrainsWrappedBuffer_ThenAllowsReuse(bool fromFront)
    {
        var buffer = new CircularBuffer<int>(3, [1, 2, 3]);
        buffer.PushBack(4);
        int[] expected = fromFront ? [2, 3, 4] : [4, 3, 2];

        foreach (var item in expected)
        {
            if (fromFront)
            {
                Assert.Equal(item, buffer.Front());
                buffer.PopFront();
            }
            else
            {
                Assert.Equal(item, buffer.Back());
                buffer.PopBack();
            }
        }

        Assert.True(buffer.IsEmpty);
        Assert.Equal(0, buffer.Size);
        Assert.Empty(buffer.ToArray());
        buffer.PushFront(8);
        buffer.PushBack(9);
        AssertContents(buffer, 8, 9);
    }

    [Fact]
    public void CapacityOne_OverwritesAtEitherEnd_AndCanBeReused()
    {
        var buffer = new CircularBuffer<int>(1);
        buffer.PushBack(1);
        buffer.PushBack(2);
        AssertContents(buffer, 2);

        buffer.PushFront(3);
        Assert.True(buffer.IsFull);
        AssertContents(buffer, 3);

        buffer.PopBack();
        Assert.True(buffer.IsEmpty);
        buffer.PushFront(4);
        AssertContents(buffer, 4);
        buffer.PopFront();
        Assert.True(buffer.IsEmpty);
    }

    [Fact]
    public void Indexer_WrappedBuffer_ReadsAndUpdatesLogicalPositions()
    {
        var buffer = new CircularBuffer<int>(3, [1, 2, 3]);
        buffer.PushBack(4);
        buffer.PushBack(5);

        Assert.Equal(3, buffer[0]);
        Assert.Equal(4, buffer[1]);
        Assert.Equal(5, buffer[2]);
        buffer[0] = 30;
        buffer[2] = 50;

        AssertContents(buffer, 30, 4, 50);
    }

    [Theory, InlineData(-1), InlineData(-2), InlineData(int.MinValue), InlineData(3), InlineData(int.MaxValue)]
    public void IndexerGet_InvalidLogicalIndex_ThrowsAfterFrontHasMoved(int index)
    {
        var buffer = new CircularBuffer<int>(4, [1, 2, 3, 4]);
        buffer.PopFront();

        Assert.Throws<IndexOutOfRangeException>(() => buffer[index]);
    }

    [Theory, InlineData(-1), InlineData(-2), InlineData(int.MinValue), InlineData(3), InlineData(int.MaxValue)]
    public void IndexerSet_InvalidLogicalIndex_ThrowsWithoutChangingContents(int index)
    {
        var buffer = new CircularBuffer<int>(4, [1, 2, 3, 4]);
        buffer.PopFront();

        Assert.Throws<IndexOutOfRangeException>(() => buffer[index] = 99);
        AssertContents(buffer, 2, 3, 4);
    }

    [Fact]
    public void ToArray_ReturnsIndependentSnapshot()
    {
        var buffer = new CircularBuffer<int>(2, [1, 2]);
        var snapshot = buffer.ToArray();
        snapshot[0] = 99;
        Assert.Equal(1, buffer.Front());

        buffer.PushBack(3);

        Assert.Equal(new[] { 99, 2 }, snapshot);
        AssertContents(buffer, 2, 3);
    }

    [Fact]
    public void ToArraySegments_WrappedBuffer_ExposesOrderedStorageWithoutCopying()
    {
        var buffer = new CircularBuffer<int>(3, [1, 2, 3]);
        buffer.PushBack(4);
        var segments = buffer.ToArraySegments();

        Assert.Equal(new[] { 2, 3, 4 }, segments.SelectMany(segment => segment));
        Assert.All(segments, segment => Assert.True(segment.Count > 0));
        segments[0].Array![segments[0].Offset] = 20;
        segments[1].Array![segments[1].Offset] = 40;

        AssertContents(buffer, 20, 3, 40);
    }

    [Fact]
    public void Pop_RemovesReferencesFromBothEnds()
    {
        var buffer = new CircularBuffer<string>(3, ["first", "middle", "last"]);
        var contents = buffer.ToArraySegments()[0];

        buffer.PopFront();
        buffer.PopBack();

        Assert.Null(contents.Array![contents.Offset]);
        Assert.Null(contents.Array[contents.Offset + contents.Count - 1]);
        AssertContents(buffer, "middle");
    }

    [Fact]
    public void Clear_WrappedBuffer_ReleasesReferencesAndKeepsCapacityForReuse()
    {
        var buffer = new CircularBuffer<string>(3, ["a", "b", "c"]);
        buffer.PushBack("d");
        var borrowedSegments = buffer.ToArraySegments();

        buffer.Clear();

        Assert.Equal(3, buffer.Capacity);
        Assert.Equal(0, buffer.Size);
        Assert.True(buffer.IsEmpty);
        Assert.False(buffer.IsFull);
        Assert.Empty(buffer.ToArray());
        Assert.All(borrowedSegments.SelectMany(segment => segment), item => Assert.Null(item));

        buffer.PushBack("e");
        buffer.PushFront("f");
        AssertContents(buffer, "f", "e");
    }

    private static void AssertContents<T>(CircularBuffer<T> buffer, params T[] expected)
    {
        Assert.Equal(expected.Length, buffer.Size);
        Assert.False(buffer.IsEmpty);
        Assert.Equal(expected[0], buffer.Front());
        Assert.Equal(expected[^1], buffer.Back());
        Assert.Equal(expected, buffer.ToArray());
        Assert.Equal(expected, buffer.ToList());
        Assert.Equal(expected, ((IEnumerable)buffer).Cast<T>().ToArray());
    }
}
