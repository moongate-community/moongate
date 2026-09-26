using Moongate.UoxItemConverter.Internal;

namespace Moongate.UoxItemConverter.Tests.Internal;

public sealed class DfnParserTests
{
    [Fact]
    public void Parse_KeepsTheCommentOfEachFieldAndTheBraceLabel()
    {
        var block = Assert.Single(
            DfnParser.Parse(["[orc]", "{ Orc things", "NAME=#//an orc", "TITLE=5052 // the Blacksmith", "STR=96 120", "}"])
        );

        Assert.Equal("#", block.Fields["NAME"]);
        Assert.Equal("an orc", block.Comments["NAME"]);
        Assert.Equal("the Blacksmith", block.Comments["title"]);
        Assert.False(block.Comments.ContainsKey("STR"));
        Assert.Equal("Orc things", block.Label);
        Assert.Equal(["an orc", "the Blacksmith", null], block.EntryComments);
    }

    [Fact]
    public void Parse_ABareLineKeepsItsComment()
    {
        var block = Assert.Single(DfnParser.Parse(["[RANDOMNAME 5]", "{", "3009//a daemon", "Imp", "}"]));

        Assert.Equal(["3009", "Imp"], block.Entries);
        Assert.Equal(["a daemon", null], block.EntryComments);
        Assert.Null(block.Label);
    }
}
