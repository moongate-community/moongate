using Moongate.Core.Extensions.Collections;
using Moongate.Tests.TestSupport.Random;

namespace Moongate.Tests.Core.Extensions.Collections;

[Collection("Global random state")]
public sealed class CollectionExtensionsTests
{
    [Fact]
    public void RandomElement_RejectsNullAndEmptyCollections()
    {
        IReadOnlyCollection<int> missing = null!;
        Assert.Throws<ArgumentException>(() => missing.RandomElement());
        Assert.Throws<ArgumentException>(() => Array.Empty<int>().RandomElement());
    }

    [Fact]
    public void RandomElement_ReturnsAnExistingMemberFromNonIndexedCollections()
    {
        using var random = new RandomStateScope();
        var singleton = new object();
        IReadOnlyCollection<object> single = new HashSet<object> { singleton };
        IReadOnlyCollection<string> choices = new HashSet<string> { "one", "two", "three" };

        Assert.Same(singleton, single.RandomElement());
        Assert.Contains(choices.RandomElement(), choices);
    }
}
