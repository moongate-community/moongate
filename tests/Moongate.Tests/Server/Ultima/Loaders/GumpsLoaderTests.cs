using Moongate.Core.Directories;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class GumpsLoaderTests
{
    private const string Confirm = """
                                   <gump id="confirm" x="100" y="50" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="gump.xsd">
                                     <background x="0" y="0" gump="9200" width="300" height="200" />
                                     <text x="20" y="20" hue="${hue}">Release ${pet_name}?</text>
                                     <page>
                                       <button x="20" y="80" up="4005" down="4007" on_click="release" />
                                       <button x="120" y="80" up="4005" down="4007" id="2" />
                                     </page>
                                   </gump>
                                   """;

    [Fact]
    public async Task LoadDataAsync_ReadsEveryGumpFile()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/gumps/confirm.xml", Confirm);
        root.CreateFile("templates/gumps/gump.xsd", "not read");

        var gump = Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities);

        Assert.Equal("confirm", gump.Id);
        Assert.EndsWith("confirm.xml", gump.File);
        Assert.Equal(["background", "text", "page"], gump.Root.Elements().Select(element => element.Name.LocalName));
    }

    [Fact]
    public async Task LoadDataAsync_NoFolder_LoadsNothing()
    {
        using var root = new TemporaryDirectory();

        Assert.Empty((await CreateLoader(root).LoadDataAsync()).Entities);
    }

    [Theory,
     InlineData("<gump id=\"a\"><text x=\"1\">no y</text></gump>", "line 1"),
     InlineData("<gump id=\"a\"><text x=\"one\" y=\"1\" /></gump>", "'x'"),
     InlineData("<gump id=\"a\"><dragon /></gump>", "dragon"),
     InlineData("<gump id=\"Bad Id\" />", "'id'"),
     InlineData("<gump id=\"a\">\n<text x=\"1\" y=\"1\" cliloc=\"5\" /></gump>", "line 2"),
     InlineData("<gump id=\"a\"", "a.xml"),
     InlineData("<gump xmlns=\"urn:x\" id=\"a\"><text x=\"one\" y=\"1\" /></gump>", "line 1"),
     InlineData(
         "<gump id=\"a\"><text_entry x=\"1\" y=\"1\" width=\"1\" height=\"1\" entry=\"1\" max_length=\"240\" /></gump>",
         "max_length"
     ),
     InlineData("<gump id=\"a\"><button x=\"1\" y=\"1\" up=\"1\" down=\"2\" id=\"99999999999\" /></gump>", "'id'"),
     InlineData("<gump id=\"a\"><checkbox x=\"1\" y=\"1\" off=\"1\" on=\"2\" switch=\"99999999999\" /></gump>", "'switch'")]
    public async Task LoadDataAsync_AFileTheSchemaRefuses_StopsWithTheFileAndLine(string xml, string expected)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/gumps/a.xml", xml);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains(expected, exception.Message);
        Assert.Contains("a.xml", exception.Message);
    }

    [Theory,
     InlineData("<button x=\"1\" y=\"1\" up=\"1\" down=\"2\" />", "one of on_click, id, page or open"),
     InlineData("<button x=\"1\" y=\"1\" up=\"1\" down=\"2\" id=\"1\" page=\"2\" />", "one of on_click, id, page or open"),
     InlineData("<button x=\"1\" y=\"1\" up=\"1\" down=\"2\" open=\"nowhere\" />", "opens gump 'nowhere'"),
     InlineData("<html x=\"1\" y=\"1\" width=\"1\" height=\"1\" cliloc=\"5\" message=\"6\" />", "cliloc or message"),
     InlineData("<html x=\"1\" y=\"1\" width=\"1\" height=\"1\" cliloc=\"5\">text</html>", "cliloc or a text"),
     InlineData("<text x=\"1\" y=\"1\" message=\"5\">text</text>", "message or a text"),
     InlineData("<html x=\"1\" y=\"1\" width=\"1\" height=\"1\" color=\"5\">text</html>", "color needs a cliloc"),
     InlineData(
         "<button x=\"1\" y=\"1\" up=\"1\" down=\"2\" id=\"3\" /><button x=\"1\" y=\"1\" up=\"1\" down=\"2\" id=\"3\" />",
         "button id 3 twice"
     ),
     InlineData(
         "<checkbox x=\"1\" y=\"1\" off=\"1\" on=\"2\" switch=\"4\" /><group><radio x=\"1\" y=\"1\" off=\"1\" on=\"2\" switch=\"4\" /></group>",
         "switch 4 twice"
     ),
     InlineData(
         "<text_entry x=\"1\" y=\"1\" width=\"1\" height=\"1\" entry=\"2\" /><text_entry x=\"1\" y=\"1\" width=\"1\" height=\"1\" entry=\"2\" />",
         "entry 2 twice"
     ),
     InlineData("<page><button x=\"1\" y=\"1\" up=\"1\" down=\"2\" page=\"2\" /></page>", "page 2, but the gump has 1"),
     InlineData("<slot name=\"rows\" x=\"1\" y=\"1\" /><page />", "a slot cannot be in a gump with pages"),
     InlineData("<button x=\"1\" y=\"1\" up=\"1\" down=\"2\" on_click=\"__x\" />", "reserved")]
    public async Task LoadDataAsync_AControlTheSchemaCannotCheck_StopsWithTheReason(string control, string expected)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/gumps/a.xml", $"<gump id=\"a\">{control}</gump>");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains(expected, exception.Message);
    }

    [Fact]
    public async Task LoadDataAsync_AButtonOpeningAnotherGump_Loads()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "templates/gumps/step1.xml",
            "<gump id=\"step1\"><text_entry x=\"1\" y=\"1\" width=\"1\" height=\"1\" entry=\"1\" bind=\"name\" /><button x=\"1\" y=\"1\" up=\"1\" down=\"2\" open=\"step2\" /></gump>"
        );
        root.CreateFile("templates/gumps/step2.xml", "<gump id=\"step2\" />");

        Assert.Equal(2, (await CreateLoader(root).LoadDataAsync()).Entities.Count);
    }

    [Fact]
    public async Task LoadDataAsync_TheSameIdTwice_Stops()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/gumps/a.xml", "<gump id=\"same\" />");
        root.CreateFile("templates/gumps/b.xml", "<gump id=\"same\" />");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("same", exception.Message);
    }

    private static GumpsLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["templates"]));
    }
}
