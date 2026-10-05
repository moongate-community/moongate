using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Tests.Server.Ultima.Entities.World;

public sealed class BulletinMessageEntityTests
{
    [Fact]
    public void Lines_SplitsTheBody()
    {
        var message = new BulletinMessageEntity { Body = "Selling a horse\n\nAsk at the stables" };

        // An empty line between two is a line.
        Assert.Equal(["Selling a horse", "", "Ask at the stables"], message.Lines());
    }

    [Fact]
    public void Lines_OfAnEmptyBody_IsNone()
    {
        Assert.Empty(new BulletinMessageEntity().Lines());
    }

    [Fact]
    public void IsThread_ForAFirstMessage_NotForAReply()
    {
        Assert.True(new BulletinMessageEntity { Id = new Serial(0x40000010) }.IsThread);
        Assert.False(new BulletinMessageEntity { Id = new Serial(0x40000011), ThreadId = new Serial(0x40000010) }.IsThread);
    }

    [Fact]
    public void Snapshot_IsADetachedCopy()
    {
        var message = new BulletinMessageEntity { Id = new Serial(0x40000010), Subject = "Horse", LastReplyAt = 1000 };

        var copy = message.Snapshot();
        copy.LastReplyAt = 2000;

        Assert.NotSame(message, copy);
        Assert.Equal((new Serial(0x40000010), "Horse", 1000L), (message.Id, message.Subject, message.LastReplyAt));
        Assert.Equal((new Serial(0x40000010), "Horse", 2000L), (copy.Id, copy.Subject, copy.LastReplyAt));
    }
}
