using Moongate.Server.Ultima.Data.Tooltips;

namespace Moongate.Tests.Server.Ultima.Data.Tooltips;

public sealed class PropertyListTests
{
    [Fact]
    public void AddText_RotatesThroughTheEmptyClilocs()
    {
        // The client shows one line per cliloc number, so each free text takes the next ~1_NOTHING~ cliloc.
        var list = new PropertyList();

        list.AddText("a");
        list.AddText("b");

        Assert.Equal([(1042971, "a"), (1070722, "b")], list.Entries.Select(entry => (entry.Cliloc, entry.Arguments)));
    }

    [Fact]
    public void Add_ALongArgument_IsCutTo504Characters()
    {
        var list = new PropertyList();

        list.Add(1050045, new string('x', 600));

        Assert.Equal(504, list.Entries.Single().Arguments.Length);
    }

    [Fact]
    public void Hash_IsTheSameForTheSameLinesAndChangesWithThem()
    {
        var first = new PropertyList();
        first.Add(1020000 + 0x0EED);
        var same = new PropertyList();
        same.Add(1020000 + 0x0EED);
        var other = new PropertyList();
        other.Add(1050039, "5\t#1023821");

        Assert.Equal(first.Hash, same.Hash);
        Assert.NotEqual(first.Hash, other.Hash);
        Assert.InRange(first.Hash, 0, 0x3FFFFFF);
    }
}
