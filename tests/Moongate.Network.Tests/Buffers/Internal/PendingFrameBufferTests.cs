using Moongate.Network.Buffers.Internal;
using Moongate.Network.Tests.Support;

namespace Moongate.Network.Tests.Buffers.Internal;

public sealed class PendingFrameBufferTests
{
    [Fact]
    public void Append_OverBudgetAfterInitialRental_RejectsBeforeGrowing()
    {
        var pool = new TrackingArrayPool();
        using var pending = new PendingFrameBuffer(
            new PendingFrameBufferTestFramer(),
            2,
            4,
            pool
        );
        pending.Append(new byte[] { 3, 10 });
        var rentsBefore = pool.RentCount;

        Assert.Throws<InvalidDataException>(() => pending.Append(new byte[5]));

        Assert.Equal(rentsBefore, pool.RentCount);
        Assert.Equal(2, pending.Length);
    }

    [Fact]
    public void Append_OverBudget_RejectsBeforeRenting()
    {
        var pool = new TrackingArrayPool();
        using var pending = new PendingFrameBuffer(
            new PendingFrameBufferTestFramer(),
            4,
            8,
            pool
        );
        var rentsBefore = pool.RentCount;

        Assert.Throws<InvalidDataException>(() => pending.Append(new byte[13]));

        Assert.Equal(rentsBefore, pool.RentCount);
        Assert.Equal(0, pending.Length);
    }

    [Theory,
     InlineData(0, 8),
     InlineData(1048577, 8),
     InlineData(4, 0),
     InlineData(4, 16777217)]
    public void Constructor_OutOfRangeLimits_RejectBeforeRenting(int receiveBufferSize, int maxFrameLength)
    {
        var pool = new TrackingArrayPool();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new PendingFrameBuffer(
                    new PendingFrameBufferTestFramer(),
                    receiveBufferSize,
                    maxFrameLength,
                    pool
                )
        );
        Assert.Equal(0, pool.RentCount);
    }

    [Fact]
    public void Dispose_AfterFramerThrows_ReturnsEachRentalOnce()
    {
        var pool = new TrackingArrayPool();
        var failure = new InvalidOperationException("Framer failed.");
        var pending = new PendingFrameBuffer(
            new PendingFrameBufferTestFramer(exception: failure),
            1,
            4,
            pool
        );
        pending.Append(new byte[] { 1 });
        pending.Append(new byte[] { 10, 20, 30 });

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => pending.TryRead(out _)));
        pending.Dispose();
        pending.Dispose();

        Assert.True(pool.RentCount >= 2);
        Assert.Equal(pool.RentCount, pool.ReturnCount);
        Assert.Equal(0, pool.OutstandingCount);
    }

    [Fact]
    public void TryRead_CoalescedFramesExceedMaxFrameLengthInTotal_AcceptsEachFrame()
    {
        using var pending = new PendingFrameBuffer(
            new PendingFrameBufferTestFramer(),
            3,
            3
        );
        pending.Append(new byte[] { 2, 10, 11, 2, 20, 21 });

        Assert.True(pending.TryRead(out var first));
        Assert.True(pending.TryRead(out var second));

        Assert.Equal(new byte[] { 2, 10, 11 }, first);
        Assert.Equal(new byte[] { 2, 20, 21 }, second);
    }

    [Fact]
    public void TryRead_CompletedFrameExceedsMaxFrameLength_Throws()
    {
        using var pending = new PendingFrameBuffer(
            new PendingFrameBufferTestFramer(5),
            4,
            4
        );
        pending.Append(new byte[] { 4, 10, 20, 30, 40 });

        Assert.Throws<InvalidDataException>(() => pending.TryRead(out _));
    }

    [Fact]
    public void TryRead_ConsumedFrame_RemainsValidAfterReuseAndDispose()
    {
        var pool = new TrackingArrayPool();
        var pending = new PendingFrameBuffer(
            new PendingFrameBufferTestFramer(),
            4,
            8,
            pool
        );
        pending.Append(new byte[] { 2, 0xAA, 0xBB });
        Assert.True(pending.TryRead(out var frame));
        pending.Append(new byte[] { 1, 0xCC });
        pending.Dispose();
        pending.Dispose();

        Assert.Equal(new byte[] { 2, 0xAA, 0xBB }, frame);
        Assert.Equal(1, pool.RentCount);
        Assert.Equal(1, pool.ReturnCount);
        Assert.Equal(0, pool.OutstandingCount);
    }

    [Fact]
    public void TryRead_FrameFragmentedAfterEveryByte_EmitsOnlyWhenComplete()
    {
        using var pending = new PendingFrameBuffer(
            new PendingFrameBufferTestFramer(),
            1,
            4
        );
        var encodedFrame = new byte[] { 3, 10, 20, 30 };

        for (var index = 0; index < encodedFrame.Length - 1; index++)
        {
            pending.Append(encodedFrame.AsSpan(index, 1));
            Assert.False(pending.TryRead(out _));
        }

        pending.Append(encodedFrame.AsSpan(encodedFrame.Length - 1, 1));

        Assert.True(pending.TryRead(out var frame));
        Assert.Equal(new byte[] { 3, 10, 20, 30 }, frame);
        Assert.Equal(0, pending.Length);
    }

    [Theory, InlineData(0), InlineData(-1), InlineData(3)]
    public void TryRead_FramerReportsNonPositiveOrUnavailableLength_Throws(int reportedLength)
    {
        using var pending = new PendingFrameBuffer(
            new PendingFrameBufferTestFramer(reportedLength),
            2,
            8
        );
        pending.Append(new byte[] { 1, 42 });

        Assert.Throws<InvalidDataException>(() => pending.TryRead(out _));
    }

    [Fact]
    public void TryRead_IncompleteFrameExceedsMaxFrameLength_ThrowsWithinFiniteBudget()
    {
        var pool = new TrackingArrayPool();
        using var pending = new PendingFrameBuffer(
            new PendingFrameBufferTestFramer(),
            2,
            4,
            pool
        );
        pending.Append(new byte[] { 6, 10, 20, 30, 40 });

        Assert.Throws<InvalidDataException>(() => pending.TryRead(out _));

        Assert.InRange(pool.LargestRequestedLength, 1, 6);
    }

    [Fact]
    public void TryRead_StatefulHeaderSurvivesGrowthAndCompaction_DecodesEachHeaderOnce()
    {
        var pool = new TrackingArrayPool();
        using var pending = new PendingFrameBuffer(
            new StatefulPendingFrameFramer(),
            1,
            8,
            pool
        );
        pending.Append(new byte[] { 0x5E });
        Assert.False(pending.TryRead(out _));

        pending.Append(new byte[] { 0xA1, 0xA2, 0xA3, 0x58, 0xB1, 0xB2 });

        Assert.True(pending.TryRead(out var first));
        Assert.True(pending.TryRead(out var second));
        Assert.Equal(new byte[] { 4, 0xA1, 0xA2, 0xA3 }, first);
        Assert.Equal(new byte[] { 3, 0xB1, 0xB2 }, second);
        Assert.True(pool.RentCount >= 2);
    }

    [Fact]
    public void TryRead_ThreeCoalescedFrames_EmitsEachFrameInOrder()
    {
        using var pending = new PendingFrameBuffer(
            new PendingFrameBufferTestFramer(),
            6,
            8
        );
        pending.Append(new byte[] { 1, 10, 1, 20, 1, 30 });

        Assert.True(pending.TryRead(out var first));
        Assert.True(pending.TryRead(out var second));
        Assert.True(pending.TryRead(out var third));
        Assert.False(pending.TryRead(out var fourth));

        Assert.Equal(new byte[] { 1, 10 }, first);
        Assert.Equal(new byte[] { 1, 20 }, second);
        Assert.Equal(new byte[] { 1, 30 }, third);
        Assert.Null(fourth);
        Assert.Equal(0, pending.Length);
    }
}
