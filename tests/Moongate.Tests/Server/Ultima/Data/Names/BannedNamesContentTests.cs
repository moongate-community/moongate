using Moongate.Server.Ultima.Data.Names;

namespace Moongate.Tests.Server.Ultima.Data.Names;

public sealed class BannedNamesContentTests
{
    private static readonly char[] Separators = [' ', '-', '.', '\''];

    private static readonly BannedNamesContent BannedNames = new()
    {
        StartsWith = ["gm"],
        Words = ["mage"]
    };

    [Theory, InlineData("GMaria"), InlineData("gm Aria"), InlineData("Aria the Mage"), InlineData("MAGE"), InlineData("Aria-mage")]
    public void IsBanned_PrefixOrWholeWord_IgnoringCase_IsTrue(string name)
    {
        Assert.True(BannedNames.IsBanned(name, Separators));
    }

    [Theory, InlineData("Magenta"), InlineData("Aria"), InlineData("Imagen"), InlineData("Algm")]
    public void IsBanned_WordInsideAnotherWordOrPrefixElsewhere_IsFalse(string name)
    {
        Assert.False(BannedNames.IsBanned(name, Separators));
    }
}
