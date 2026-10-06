using System.Xml.Linq;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Gumps;
using Moongate.Tests.TestSupport.Localization;

namespace Moongate.Tests.Server.Ultima.Services.Internal;

public sealed class GumpXmlRendererTests
{
    private static readonly Dictionary<string, string> NoArgs = [];

    [Fact]
    public void Render_WritesTheControlsOfPageZeroAndThePages()
    {
        var rendered = Render(
            """
            <gump id="a" x="100" y="${top}">
              <background x="0" y="0" gump="9200" width="300" height="200" />
              <alpha_region x="1" y="2" width="3" height="4" />
              <image x="1" y="2" gump="5" hue="33" />
              <image_tiled x="1" y="2" width="3" height="4" gump="5" />
              <item x="1" y="2" item="3821" />
              <page>
                <checkbox x="1" y="2" off="210" on="211" switch="7" checked="true" />
                <group>
                  <radio x="1" y="2" off="208" on="209" switch="8" />
                  <radio x="1" y="2" off="208" on="209" switch="9" />
                </group>
                <button x="1" y="2" up="3" down="4" page="2" />
              </page>
              <page>
                <tooltip cliloc="1011036" />
                <item_property serial="${serial}" />
              </page>
            </gump>
            """,
            new() { ["top"] = "50", ["serial"] = "1073741825" }
        );

        Assert.Equal((100, 50), (rendered.X, rendered.Y));
        Assert.Equal(
            "{ page 0 }{ resizepic 0 0 9200 300 200 }{ checkertrans 1 2 3 4 }{ gumppic 1 2 5 hue=33 }" +
            "{ gumppictiled 1 2 3 4 5 }{ tilepic 1 2 3821 }" +
            "{ page 1 }{ checkbox 1 2 210 211 1 7 }{ group 1 }{ radio 1 2 208 209 0 8 }{ radio 1 2 208 209 0 9 }" +
            "{ button 1 2 3 4 0 2 0 }" +
            "{ page 2 }{ tooltip 1011036 }{ itemproperty 1073741825 }",
            rendered.Layout.Build().Layout
        );
    }

    [Fact]
    public void Render_TheGumpFlags_FollowTheAttributesSetToFalse()
    {
        var rendered = Render("""<gump id="a" closable="false" movable="false" disposable="false" resizable="false" />""");

        Assert.Equal("{ noclose }{ nomove }{ nodispose }{ noresize }{ page 0 }", rendered.Layout.Build().Layout);
    }

    [Fact]
    public void Render_Texts_ComeFromTheElement_AServerMessage_OrAClientMessage()
    {
        var localization = TestLocalization.With((30090, "Libera ${pet_name}"));

        var rendered = GumpXmlRenderer.Render(
            Template(
                """
                <gump id="a">
                  <text x="1" y="2" hue="5">Hello ${pet_name}</text>
                  <text x="1" y="2" message="30090" />
                  <label_cropped x="1" y="2" width="3" height="4">Cut</label_cropped>
                  <html x="1" y="2" width="3" height="4" scrollbar="true">&lt;b&gt;Bold&lt;/b&gt;</html>
                  <html x="1" y="2" width="3" height="4" cliloc="1046257" />
                  <html x="1" y="2" width="3" height="4" cliloc="1011011" color="32767" />
                  <html x="1" y="2" width="3" height="4" cliloc="1070722" args="${pet_name}" />
                  <text_entry x="1" y="2" width="3" height="4" entry="5" max_length="20">${pet_name}</text_entry>
                </gump>
                """
            ),
            new Dictionary<string, string> { ["pet_name"] = "Fido" },
            localization
        );

        var built = rendered.Layout.Build();
        Assert.Equal(
            "{ page 0 }{ text 1 2 5 0 }{ text 1 2 0 1 }{ croppedtext 1 2 3 4 0 2 }{ htmlgump 1 2 3 4 3 0 1 }" +
            "{ xmfhtmlgump 1 2 3 4 1046257 0 0 }{ xmfhtmlgumpcolor 1 2 3 4 1011011 0 0 32767 }" +
            "{ xmfhtmltok 1 2 3 4 0 0 0 1070722 @Fido@ }{ textentrylimited 1 2 3 4 0 5 4 20 }",
            built.Layout
        );
        Assert.Equal(["Hello Fido", "Libera Fido", "Cut", "<b>Bold</b>", "Fido"], built.Strings);
    }

    [Fact]
    public void Render_OnClickButtons_GetIdsTheExplicitOnesDoNotUse()
    {
        var rendered = Render(
            """
            <gump id="a">
              <button x="0" y="0" up="1" down="2" on_click="first" />
              <button x="0" y="0" up="1" down="2" id="1" />
              <button x="0" y="0" up="1" down="2" on_click="second" />
            </gump>
            """
        );

        Assert.Equal(
            "{ page 0 }{ button 0 0 1 2 1 0 2 }{ button 0 0 1 2 1 0 1 }{ button 0 0 1 2 1 0 3 }",
            rendered.Layout.Build().Layout
        );
        Assert.Equal(new Dictionary<int, string> { [2] = "first", [3] = "second" }, rendered.Clicks);
    }

    [Fact]
    public void Render_AMissingArgumentOrMessage_LeavesItEmpty_AndABadNumberIsZero()
    {
        var rendered = Render(
            """
            <gump id="a">
              <text x="${nowhere}" y="1">[${nowhere}]</text>
              <text x="1" y="1" message="99999" />
            </gump>
            """,
            new() { ["x"] = "not a number" }
        );

        var built = rendered.Layout.Build();
        Assert.Equal("{ page 0 }{ text 0 1 0 0 }{ text 1 1 0 1 }", built.Layout);
        Assert.Equal(["[]", ""], built.Strings);
    }

    [Fact]
    public void Render_OpenButtonsAndBoundControls_AreListed()
    {
        var rendered = Render(
            """
            <gump id="a">
              <text_entry x="0" y="0" width="1" height="1" entry="3" bind="name" />
              <checkbox x="0" y="0" off="1" on="2" switch="4" bind="hardcore" />
              <group>
                <radio x="0" y="0" off="1" on="2" switch="5" bind="city" />
                <radio x="0" y="0" off="1" on="2" switch="6" bind="city" />
              </group>
              <button x="0" y="0" up="1" down="2" open="step2" />
            </gump>
            """
        );

        Assert.Equal(new Dictionary<int, string> { [1] = "step2" }, rendered.Opens);
        Assert.Equal(
            [
                ("name", GumpBindType.Text, 3), ("hardcore", GumpBindType.Checkbox, 4), ("city", GumpBindType.Radio, 5),
                ("city", GumpBindType.Radio, 6)
            ],
            rendered.Binds.Select(bind => (bind.Name, bind.Kind, bind.Id))
        );
    }

    [Fact]
    public void Render_PlaceholdersInHtml_AreEscaped_AndTheAuthorsHtmlIsKept()
    {
        var rendered = Render(
            """
            <gump id="a">
              <html x="1" y="1" width="1" height="1">&lt;b&gt;Hi&lt;/b&gt; ${name}</html>
              <html x="1" y="1" width="1" height="1" cliloc="1070722" args="${name}" />
              <text x="1" y="1">${name}</text>
            </gump>
            """,
            new() { ["name"] = "<a href=x>Aria</a>" }
        );

        var built = rendered.Layout.Build();
        Assert.Equal(["<b>Hi</b> &lt;a href=x&gt;Aria&lt;/a&gt;", "<a href=x>Aria</a>"], built.Strings);

        var quoted = Render(
            """<gump id="a"><html x="1" y="1" width="1" height="1">&lt;a href="${link}"&gt;go&lt;/a&gt;</html></gump>""",
            new() { ["link"] = "x\" onclick=\"y" }
        );
        Assert.Equal("<a href=\"x&quot; onclick=&quot;y\">go</a>", quoted.Layout.Build().Strings[0]);
        Assert.Contains("@&lt;a href=x&gt;Aria&lt;/a&gt;@", built.Layout);
    }

    [Fact]
    public void Render_TextsLoseTheIndentationOfTheFile()
    {
        var rendered = Render(
            """
            <gump id="a">
              <html x="1" y="1" width="1" height="1">
                Line one
                Line two
              </html>
              <text x="1" y="1">   padded   </text>
            </gump>
            """
        );

        Assert.Equal(["Line one Line two", "padded"], rendered.Layout.Build().Strings);
    }

    private static Moongate.Server.Ultima.Data.Gumps.RenderedGump Render(string xml, Dictionary<string, string>? args = null)
    {
        return GumpXmlRenderer.Render(Template(xml), args ?? NoArgs, null);
    }

    private static GumpTemplate Template(string xml)
    {
        var root = XElement.Parse(xml);

        return new() { Id = (string)root.Attribute("id")!, File = "a.xml", Root = root };
    }
}
