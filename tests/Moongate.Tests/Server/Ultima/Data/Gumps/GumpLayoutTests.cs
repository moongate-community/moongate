using System.Globalization;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Types.Gumps;

namespace Moongate.Tests.Server.Ultima.Data.Gumps;

public sealed class GumpLayoutTests
{
    public static TheoryData<GumpEntry, string> Entries => new()
    {
        { new GumpPage { Page = 2 }, "{ page 2 }" },
        { new GumpGroup { Group = 1 }, "{ group 1 }" },
        { new GumpBackground { X = 0, Y = 5, GumpId = 9200, Width = 300, Height = 400 }, "{ resizepic 0 5 9200 300 400 }" },
        { new GumpAlphaRegion { X = 10, Y = 10, Width = 280, Height = 380 }, "{ checkertrans 10 10 280 380 }" },
        { new GumpImage { X = 1, Y = 2, GumpId = 5 }, "{ gumppic 1 2 5 }" },
        { new GumpImage { X = 1, Y = 2, GumpId = 5, Hue = 33 }, "{ gumppic 1 2 5 hue=33 }" },
        { new GumpImageTiled { X = 1, Y = 2, Width = 30, Height = 40, GumpId = 2624 }, "{ gumppictiled 1 2 30 40 2624 }" },
        { new GumpItem { X = 1, Y = 2, ItemId = 0x0EED }, "{ tilepic 1 2 3821 }" },
        { new GumpItem { X = 1, Y = 2, ItemId = 0x0EED, Hue = 1153 }, "{ tilepichue 1 2 3821 1153 }" },
        { new GumpButton { X = 20, Y = 80, Up = 4005, Down = 4007, ButtonId = 2 }, "{ button 20 80 4005 4007 1 0 2 }" },
        { new GumpButton { X = 20, Y = 80, Up = 4005, Down = 4007, Page = 3 }, "{ button 20 80 4005 4007 0 3 0 }" },
        { new GumpCheckbox { X = 1, Y = 2, Off = 210, On = 211, Checked = true, SwitchId = 100 }, "{ checkbox 1 2 210 211 1 100 }" },
        { new GumpRadio { X = 1, Y = 2, Off = 208, On = 209, SwitchId = 7 }, "{ radio 1 2 208 209 0 7 }" },
        { new GumpHtmlLocalized { X = 1, Y = 2, Width = 3, Height = 4, Cliloc = 1046257, Background = true, Scrollbar = true }, "{ xmfhtmlgump 1 2 3 4 1046257 1 1 }" },
        { new GumpHtmlLocalized { X = 1, Y = 2, Width = 3, Height = 4, Cliloc = 1011011, Color = 0x7FFF }, "{ xmfhtmlgumpcolor 1 2 3 4 1011011 0 0 32767 }" },
        { new GumpHtmlLocalized { X = 1, Y = 2, Width = 3, Height = 4, Cliloc = 1070000, Args = "Fido\tTwo" }, "{ xmfhtmltok 1 2 3 4 0 0 0 1070000 @Fido\tTwo@ }" },
        { new GumpTooltip { Cliloc = 1011036 }, "{ tooltip 1011036 }" },
        { new GumpTooltip { Cliloc = 1070722, Args = "Hello" }, "{ tooltip 1070722 @Hello@ }" },
        { new GumpItemProperty { Serial = 0x40000001 }, "{ itemproperty 1073741825 }" },
        { new GumpFlag { Flag = GumpFlagType.NoMove }, "{ nomove }" },
        { new GumpFlag { Flag = GumpFlagType.NoClose }, "{ noclose }" },
        { new GumpFlag { Flag = GumpFlagType.NoDispose }, "{ nodispose }" },
        { new GumpFlag { Flag = GumpFlagType.NoResize }, "{ noresize }" }
    };

    [Theory, MemberData(nameof(Entries))]
    public void AnEntry_IsWrittenAsTheClientReadsIt(GumpEntry entry, string expected)
    {
        var layout = new GumpLayout();
        layout.Add(entry);

        Assert.Equal(expected, layout.Build().Layout);
    }

    [Fact]
    public void Numbers_AreWrittenTheSameWhateverTheServerLanguage()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("sv-SE");

        try
        {
            var layout = new GumpLayout().Add(new GumpPage { Page = 1 }).Add(new GumpImage { X = -10, Y = -2, GumpId = 5, Hue = 33 });

            Assert.Equal("{ page 1 }{ gumppic -10 -2 5 hue=33 }", layout.Build().Layout);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void TextEntries_PointIntoTheStringTable_SharingRepeatedStrings()
    {
        var layout = new GumpLayout();
        layout.Add(new GumpText { X = 1, Y = 2, Hue = 0, Text = "Hello" });
        layout.Add(new GumpLabelCropped { X = 1, Y = 2, Width = 3, Height = 4, Hue = 5, Text = "World" });
        layout.Add(new GumpHtml { X = 1, Y = 2, Width = 3, Height = 4, Text = "<b>Hello</b>", Scrollbar = true });
        layout.Add(new GumpText { X = 9, Y = 9, Hue = 0, Text = "Hello" });
        layout.Add(new GumpTextEntry { X = 1, Y = 2, Width = 3, Height = 4, Hue = 0, EntryId = 5, Text = "" });
        layout.Add(new GumpTextEntry { X = 1, Y = 2, Width = 3, Height = 4, Hue = 0, EntryId = 6, Text = "World", MaxLength = 20 });

        var built = layout.Build();

        Assert.Equal(
            "{ text 1 2 0 0 }{ croppedtext 1 2 3 4 5 1 }{ htmlgump 1 2 3 4 2 0 1 }{ text 9 9 0 0 }" +
            "{ textentry 1 2 3 4 0 5 3 }{ textentrylimited 1 2 3 4 0 6 1 20 }",
            built.Layout
        );
        Assert.Equal(["Hello", "World", "<b>Hello</b>", ""], built.Strings);
    }

    [Fact]
    public void Build_TellsWhatTheClientMayAnswer()
    {
        var layout = new GumpLayout();
        layout.Add(new GumpButton { X = 0, Y = 0, Up = 1, Down = 2, ButtonId = 4 });
        layout.Add(new GumpButton { X = 0, Y = 0, Up = 1, Down = 2, Page = 2 });
        layout.Add(new GumpCheckbox { X = 0, Y = 0, Off = 1, On = 2, SwitchId = 10 });
        layout.Add(new GumpRadio { X = 0, Y = 0, Off = 1, On = 2, SwitchId = 11 });
        layout.Add(new GumpTextEntry { X = 0, Y = 0, Width = 1, Height = 1, EntryId = 3, Text = "" });

        var built = layout.Build();

        Assert.Equal([4], built.Buttons);
        Assert.Equal([10, 11], built.Switches.Order());
        Assert.Equal([3], built.TextEntries);
    }
}
