using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Services.Books;

namespace Moongate.Tests.Server.Ultima.Services.Books;

public sealed class BookGumpRendererTests
{
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
