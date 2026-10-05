using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Services.Books;

namespace Moongate.Tests.Server.Ultima.Services.Books;

public sealed class BookGumpRendererTests
{
    [Fact]
    public void TryBuild_ClaimFooterEscapesTextAndFitsBothPackets()
    {
        Assert.True(BookGumpRenderer.TryBuild("Title", "Author", "Body", "Claim <&>\"\nNow", (_, _) => { }, out var gump));
        var built = gump!.Layout.Build();
        Assert.Contains(1, built.Buttons);
        Assert.Contains("Claim &lt;&amp;&gt;&quot;<br>Now", built.Strings);
        Assert.Equal(285, Assert.Single(gump.Layout.Entries.OfType<GumpHtml>(), html => html.Scrollbar).Height);
        _ = new Moongate.Server.Ultima.Packets.Gumps.GumpPacket(1, 1, 0, 0, built);
        _ = new Moongate.Server.Ultima.Packets.Gumps.CompressedGumpPacket(1, 1, 0, 0, built);
        Assert.False(BookGumpRenderer.TryBuild("Title", "", new string('&', 16384), "Claim", (_, _) => { }, out _));
    }

    [Fact]
    public void TryBuild_PlainText_EscapesOnlyAtPresentationAndPreservesParagraphs()
    {
        Assert.True(BookGumpRenderer.TryBuild("Title &<>\"", "By &<>\"", "First &<>\"\r\n\r\nLast", out var gump));
        Assert.NotNull(gump);
        var built = gump.Layout.Build();
        Assert.Equal(["Title &amp;&lt;&gt;&quot;", "By &amp;&lt;&gt;&quot;", "First &amp;&lt;&gt;&quot;<br><br>Last"], built.Strings);
        Assert.Contains("{ resizepic 0 0 9380 ", built.Layout);
        Assert.True(Assert.Single(gump.Layout.Entries.OfType<GumpHtml>(), html => html.Height > 100).Scrollbar);
    }

    [Fact]
    public void TryBuild_EscapingInflatesPacket_RefusesWithoutTruncating()
    {
        Assert.False(BookGumpRenderer.TryBuild("Note", "", new string('&', 16384), out var gump));
        Assert.Null(gump);
        Assert.True(BookGumpRenderer.TryBuild("Note", "", new string('x', 16384), out _));
    }
}
