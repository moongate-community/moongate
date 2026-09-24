using System.Collections;
using Moongate.Core.Collections;

namespace Moongate.Tests.Core.Collections;

public class ArrayEnumeratorTests
{
    [Fact]
    public void Current_AfterIterationEnds_Throws()
    {
        IEnumerator enumerator = new ArrayEnumerator<int>([4]);
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());

        Assert.Throws<InvalidOperationException>(() => enumerator.Current);
    }

    [Fact]
    public void MoveNext_EmptyArray_EndsWithoutAnItem()
    {
        IEnumerator enumerator = new ArrayEnumerator<int>([]);

        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => enumerator.Current);
        enumerator.Reset();
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_VisitsEveryItemIncludingNull_AndRemainsEnded()
    {
        using var enumerator = new ArrayEnumerator<string?>(["first", null, "last"]);
        var visited = new List<string?>();

        while (enumerator.MoveNext())
        {
            visited.Add(enumerator.Current);
        }

        Assert.Equal(new[] { "first", null, "last" }, visited);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Reset_AfterPartialIteration_StartsAgainFromFirstItem()
    {
        IEnumerator enumerator = new ArrayEnumerator<int>([4, 8]);
        Assert.Throws<InvalidOperationException>(() => enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(4, enumerator.Current);

        enumerator.Reset();

        Assert.Throws<InvalidOperationException>(() => enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(4, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(8, enumerator.Current);
    }
}
