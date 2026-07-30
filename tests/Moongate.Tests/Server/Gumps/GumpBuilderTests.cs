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

    [Fact]
    public void Group_WritesGroup()
    {
        var builder = new GumpBuilder();

        builder.AddGroup(1);

        Assert.Equal("{ group 1 }", builder.Layout);
    }

    [Fact]
    public void AlphaRegion_WritesCheckerTrans()
    {
        var builder = new GumpBuilder();

        builder.AddAlphaRegion(10, 10, 100, 50);

        Assert.Equal("{ checkertrans 10 10 100 50 }", builder.Layout);
    }

    [Fact]
    public void Radio_RecordsItsSwitchId()
    {
        var builder = new GumpBuilder();

        builder.AddRadio(10, 10, 208, 209, false, 4);

        Assert.Equal("{ radio 10 10 208 209 0 4 }", builder.Layout);
        Assert.Equal([4], builder.SwitchIds);
    }

    [Fact]
    public void LabelCropped_WritesCroppedText()
    {
        var builder = new GumpBuilder();

        builder.AddLabelCropped(10, 10, 80, 20, 1153, "clipped");

        Assert.Equal("{ croppedtext 10 10 80 20 1153 0 }", builder.Layout);
        Assert.Equal(["clipped"], builder.Strings);
    }

    [Fact]
    public void Html_WritesBackgroundAndScrollbarAsFlags()
    {
        var builder = new GumpBuilder();

        builder.AddHtml(10, 10, 200, 100, "body", true, false);

        Assert.Equal("{ htmlgump 10 10 200 100 0 1 0 }", builder.Layout);
        Assert.Equal(["body"], builder.Strings);
    }

    // AddLabelHtml is a label drawn through the html renderer, so its styling is markup around the
    // text rather than parameters on the command.
    [Fact]
    public void LabelHtml_WrapsTheTextInBaseFontMarkup()
    {
        var builder = new GumpBuilder();

        builder.AddLabelHtml(10, 10, 200, 20, "titolo", "#FFFFFF", 4, true);

        Assert.Equal("{ htmlgump 10 10 200 20 0 0 0 }", builder.Layout);
        Assert.Equal(["<center><basefont color=#FFFFFF size=4>titolo</basefont></center>"], builder.Strings);
    }

    // The three localized forms differ only by which of args and color they carry; the named-table
    // Lua surface picks between them by what it was given.
    [Fact]
    public void HtmlLocalized_PlainFormWritesXmfHtmlGump()
    {
        var builder = new GumpBuilder();

        builder.AddHtmlLocalized(10, 10, 200, 100, 1049644, null, null, false, true);

        Assert.Equal("{ xmfhtmlgump 10 10 200 100 1049644 0 1 }", builder.Layout);
    }

    [Fact]
    public void HtmlLocalized_WithColourWritesXmfHtmlGumpColor()
    {
        var builder = new GumpBuilder();

        builder.AddHtmlLocalized(10, 10, 200, 100, 1049644, null, 0x7FFF, false, true);

        Assert.Equal("{ xmfhtmlgumpcolor 10 10 200 100 1049644 0 1 32767 }", builder.Layout);
    }

    // Note the order: xmfhtmltok puts the flags and colour before the cliloc, unlike the other two.
    [Fact]
    public void HtmlLocalized_WithArgumentsWritesXmfHtmlTok()
    {
        var builder = new GumpBuilder();

        builder.AddHtmlLocalized(10, 10, 200, 100, 1049644, "Squid", 0x7FFF, false, true);

        Assert.Equal("{ xmfhtmltok 10 10 200 100 0 1 32767 1049644 @Squid@ }", builder.Layout);
    }

    [Fact]
    public void Image_AppendsTheHueOnlyWhenItIsSet()
    {
        var builder = new GumpBuilder();

        builder.AddImage(10, 10, 100, 0);
        builder.AddImage(20, 20, 100, 33);

        Assert.Equal("{ gumppic 10 10 100 }{ gumppic 20 20 100 hue=33 }", builder.Layout);
    }

    [Fact]
    public void ImageTiled_WritesGumpPicTiled()
    {
        var builder = new GumpBuilder();

        builder.AddImageTiled(10, 10, 200, 100, 2624);

        Assert.Equal("{ gumppictiled 10 10 200 100 2624 }", builder.Layout);
    }

    [Fact]
    public void ImageTiledButton_RecordsItsButtonId()
    {
        var builder = new GumpBuilder();

        builder.AddImageTiledButton(10, 10, 4005, 4007, 9, 1, 0, 0x0EED, 0, 20, 20);

        Assert.Equal("{ buttontileart 10 10 4005 4007 1 0 9 3821 0 20 20 }", builder.Layout);
        Assert.Equal([9], builder.ButtonIds);
    }

    // A plain tilepic and a hued one are different commands, not one command with a default.
    [Fact]
    public void Item_UsesTilePicHueOnlyWhenHued()
    {
        var builder = new GumpBuilder();

        builder.AddItem(10, 10, 0x0EED, 0);
        builder.AddItem(20, 20, 0x0EED, 33);

        Assert.Equal("{ tilepic 10 10 3821 }{ tilepichue 20 20 3821 33 }", builder.Layout);
    }

    [Fact]
    public void SpriteImage_WritesPicInPic()
    {
        var builder = new GumpBuilder();

        builder.AddSpriteImage(10, 10, 5054, 100, 50, 4, 8);

        Assert.Equal("{ picinpic 10 10 5054 100 50 4 8 }", builder.Layout);
    }

    [Fact]
    public void Tooltip_AppendsArgumentsOnlyWhenPresent()
    {
        var builder = new GumpBuilder();

        builder.AddTooltip(1049644, null);
        builder.AddTooltip(1049644, "Squid");

        Assert.Equal("{ tooltip 1049644 }{ tooltip 1049644 @Squid@ }", builder.Layout);
    }

    [Fact]
    public void ItemProperty_WritesTheSerial()
    {
        var builder = new GumpBuilder();

        builder.AddItemProperty(0x40000001);

        Assert.Equal("{ itemproperty 1073741825 }", builder.Layout);
    }

    [Fact]
    public void GumpIdOverride_WritesMasterGump()
    {
        var builder = new GumpBuilder();

        builder.AddGumpIdOverride(3);

        Assert.Equal("{ mastergump 3 }", builder.Layout);
    }
}
