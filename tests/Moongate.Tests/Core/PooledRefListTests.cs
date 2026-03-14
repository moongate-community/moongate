using Moongate.Core.Collections;

namespace Moongate.Tests.Core;

public class PooledRefListTests
{
    [Test]
    public void Create_Add_Indexer_AndDispose_ShouldWork()
    {
        using var list = PooledRefList<string>.Create();

        list.Add("one");
        list.Add("two");

        Assert.That(list.Count, Is.EqualTo(2));
        Assert.That(list[0], Is.EqualTo("one"));
        Assert.That(list[1], Is.EqualTo("two"));
    }
}
