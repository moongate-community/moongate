using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Tests.Server.Ultima.Entities.World;

public sealed class JailSentenceEntityTests
{
    [Theory, InlineData(999, false), InlineData(1000, true), InlineData(1001, true)]
    public void IsOver_FromItsEndOn(long now, bool over)
    {
        Assert.Equal(over, new JailSentenceEntity { ReleaseAt = 1000 }.IsOver(now));
    }

    [Fact]
    public void Snapshot_IsADetachedCopy()
    {
        var sentence = new JailSentenceEntity { Id = new Serial(7), Name = "Gino", Cell = 2, Days = 3, ReleaseAt = 1000 };

        var copy = sentence.Snapshot();
        copy.Cell = 9;

        Assert.NotSame(sentence, copy);
        Assert.Equal((new Serial(7), "Gino", 2, 3, 1000L), (sentence.Id, sentence.Name, sentence.Cell, sentence.Days, sentence.ReleaseAt));
        Assert.Equal(9, copy.Cell);
    }
}
