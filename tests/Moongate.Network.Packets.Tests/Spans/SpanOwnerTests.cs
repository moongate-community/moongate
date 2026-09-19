using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Tests.Spans;

public class SpanOwnerTests
{
    [Fact]
    public void Dispose_RepeatedThroughAlias_ClearsAccessToReturnedBuffer()
    {
        var writer = new SpanWriter(8, true);
        writer.Write((byte)0xAB);
        var owner = writer.ToSpan();
        var alias = owner;

        owner.Dispose();

        Assert.True(owner.Span.IsEmpty);
        Assert.True(alias.Span.IsEmpty);
        alias.Dispose();
        Assert.True(alias.Span.IsEmpty);
    }
}
