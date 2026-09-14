using Moongate.Core.Collections;

namespace Moongate.Tests.Core.Collections;

public class CollectionHelpersTests
{
    [Fact]
    public void AddNotNull_SkipsNullAndPreservesNonNullItemsIncludingDuplicates()
    {
        ICollection<string> collection = new List<string> { "first" };

        collection.AddNotNull(null!);
        collection.AddNotNull("second");
        collection.AddNotNull("first");

        Assert.Equal(new[] { "first", "second", "first" }, collection);
    }
}
