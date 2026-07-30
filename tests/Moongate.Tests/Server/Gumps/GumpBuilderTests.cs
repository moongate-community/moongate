using Moongate.Server.Services.Gumps;

namespace Moongate.Tests.Server.Gumps;

/// <summary>
/// The builder produces two things a gump needs and nothing else: the layout command string and the
/// strings block it indexes into. Every element is one command, so each test is one command.
/// </summary>
public class GumpBuilderTests
{
    [Fact]
    public void Background_WritesResizePic()
    {
        var builder = new GumpBuilder();

        builder.AddBackground(0, 0, 300, 200, 5054);

        Assert.Equal("{ resizepic 0 0 5054 300 200 }", builder.Layout);
        Assert.Empty(builder.Strings);
    }

    // Pages cost the server nothing: a button with type 0 switches page client-side and sends no
    // packet, so a multi-page gump never round-trips.
    [Fact]
    public void Page_WritesPage()
    {
        var builder = new GumpBuilder();

        builder.AddPage(2);

        Assert.Equal("{ page 2 }", builder.Layout);
    }

    // A label's text does not travel in the layout: the layout carries the string's index and the
    // text goes in the strings block. That indirection is why the builder owns both.
    [Fact]
    public void Label_PushesTheTextAndReferencesItByIndex()
    {
        var builder = new GumpBuilder();

        builder.AddLabel(20, 20, 1153, "Bank of Britain");

        Assert.Equal("{ text 20 20 1153 0 }", builder.Layout);
        Assert.Equal(["Bank of Britain"], builder.Strings);
    }

    [Fact]
    public void Strings_AreDeduplicated()
    {
        var builder = new GumpBuilder();

        builder.AddLabel(0, 0, 0, "same");
        builder.AddLabel(0, 20, 0, "same");

        Assert.Equal("{ text 0 0 0 0 }{ text 0 20 0 0 }", builder.Layout);
        Assert.Single(builder.Strings);
    }

    // These three sets are the whole point of the builder for the service: they are what a response
    // is validated against.
    [Fact]
    public void Ids_AreRecordedPerKind()
    {
        var builder = new GumpBuilder();

        builder.AddButton(0, 0, 4005, 4007, 7, 1, 0);
        builder.AddCheck(0, 20, 210, 211, true, 3);
        builder.AddTextEntry(0, 40, 100, 20, 0, 5, "hi");

        Assert.Equal([7], builder.ButtonIds);
        Assert.Equal([3], builder.SwitchIds);
        Assert.Equal([5], builder.TextEntryIds);
    }

    // The wire order is not the argument order: type and param come before the button id.
    [Fact]
    public void Button_WritesTypeAndParamBeforeTheId()
    {
        var builder = new GumpBuilder();

        builder.AddButton(250, 60, 4005, 4007, 7, 1, 0);

        Assert.Equal("{ button 250 60 4005 4007 1 0 7 }", builder.Layout);
    }

    [Fact]
    public void Check_WritesTheInitialStateAsZeroOrOne()
    {
        var builder = new GumpBuilder();

        builder.AddCheck(20, 20, 210, 211, true, 3);

        Assert.Equal("{ checkbox 20 20 210 211 1 3 }", builder.Layout);
    }

    [Fact]
    public void TextEntry_ReferencesItsInitialTextByIndex()
    {
        var builder = new GumpBuilder();

        builder.AddTextEntry(20, 100, 200, 20, 0, 5, "hi");

        Assert.Equal("{ textentry 20 100 200 20 0 5 0 }", builder.Layout);
        Assert.Equal(["hi"], builder.Strings);
    }
}
