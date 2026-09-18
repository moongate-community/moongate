using Moongate.Core.Attributes.Entities;

namespace Moongate.Tests.Core.Attributes.Entities;

public sealed class PersistenceCollectionAttributeTests
{
    [Fact]
    public void Constructor_KeepsTheNameItWasGiven()
    {
        Assert.Equal("accounts", new PersistenceCollectionAttribute("accounts").Name);
    }

    [Theory, InlineData(""), InlineData("   ")]
    public void Constructor_BlankName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => new PersistenceCollectionAttribute(name));
    }

    [Fact]
    public void Constructor_NullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PersistenceCollectionAttribute(null!));
    }
}
