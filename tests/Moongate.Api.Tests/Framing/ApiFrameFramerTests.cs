using Moongate.Api.Exceptions;
using Moongate.Api.Framing;

namespace Moongate.Api.Tests.Framing;

public sealed class ApiFrameFramerTests
{
    [Fact]
    public void TryReadFrame_CoalescedFrames_ConsumesOnlyFirst()
    {
        var bytes = Convert.FromHexString("00000001C000000001C0");
        var framer = new ApiFrameFramer(1);
        Assert.True(framer.TryReadFrame(bytes, out var consumed));
        Assert.Equal(5, consumed);
        Assert.True(framer.TryReadFrame(bytes.AsSpan(consumed), out consumed));
        Assert.Equal(5, consumed);
    }

    [Fact]
    public void TryReadFrame_FragmentedFrame_WaitsForEveryByte()
    {
        var bytes = Convert.FromHexString("000000099501012A64C4029107");
        var framer = new ApiFrameFramer(9);

        for (var size = 0; size < bytes.Length; size++)
        {
            Assert.False(framer.TryReadFrame(bytes.AsSpan(0, size), out _));
        }

        Assert.True(framer.TryReadFrame(bytes, out var consumed));
        Assert.Equal(13, consumed);
    }

    [Theory, InlineData("00000000"), InlineData("FFFFFFFF"), InlineData("00010001")]
    public void TryReadFrame_InvalidLength_RejectsWithoutPayload(string hex)
    {
        var framer = new ApiFrameFramer(65536);
        Assert.Throws<ApiProtocolException>(() => framer.TryReadFrame(Convert.FromHexString(hex), out _));
    }
}
