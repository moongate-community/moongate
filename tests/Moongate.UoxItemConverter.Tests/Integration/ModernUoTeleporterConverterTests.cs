using Moongate.UoxItemConverter.Internal;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.UoxItemConverter.Tests.Integration;

public sealed class ModernUoTeleporterConverterTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "moongate-modernuo-teleporters-" + Guid.NewGuid().ToString("N")
    );

    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string Source => Path.Combine(_root, "teleporters.json");

    private string Destination => Path.Combine(_root, "decorations");

    private string CombinedOutput => _output + _error.ToString();

    public ModernUoTeleporterConverterTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Run_ATeleporter_GoesToTheFolderOfItsMap_WithItsDestination()
    {
        Write(Entry("Felucca", 311, 786, -24, "Felucca", 314, 784, 0));

        Assert.True(Run() == 0, CombinedOutput);

        var block = Assert.Single(Read("felucca"));
        Assert.Equal("Teleporter", block["type"]);
        Assert.Equal(0x1BC3L, block["item_id"]);
        var props = (TomlTable)block["props"];
        Assert.Equal([314L, 784L, 0L], ((TomlArray)props["point_dest"]).Cast<long>());
        Assert.False(props.ContainsKey("map_dest"));
        Assert.Equal([[311L, 786L, -24L]], Locations(block));
        Assert.Contains("felucca/teleporters.toml: 1 teleporters in 1 blocks", _output.ToString());
    }

    [Fact]
    public void Run_EveryMap_HasItsFolder()
    {
        Write(
            Entry("Felucca", 1, 1, 0, "Felucca", 2, 2, 0),
            Entry("Trammel", 1, 1, 0, "Trammel", 2, 2, 0),
            Entry("Ilshenar", 1, 1, 0, "Ilshenar", 2, 2, 0),
            Entry("Malas", 1, 1, 0, "Malas", 2, 2, 0),
            Entry("Tokuno", 1, 1, 0, "Tokuno", 2, 2, 0),
            Entry("TerMur", 1, 1, 0, "TerMur", 2, 2, 0)
        );

        Assert.True(Run() == 0, CombinedOutput);

        foreach (var folder in new[] { "felucca", "trammel", "ilshenar", "malas", "tokuno", "termur" })
        {
            Assert.Single(Read(folder));
        }
    }

    [Fact]
    public void Run_TeleportersWithTheSameDestination_ShareOneBlock()
    {
        Write(
            Entry("Trammel", 10, 10, 0, "Trammel", 50, 60, 5),
            Entry("Trammel", 11, 10, 0, "Trammel", 50, 60, 5),
            Entry("Trammel", 12, 10, 0, "Trammel", 51, 60, 5)
        );

        Assert.True(Run() == 0, CombinedOutput);

        var blocks = Read("trammel");
        Assert.Equal(2, blocks.Count);
        Assert.Equal([[10L, 10L, 0L], [11L, 10L, 0L]], Locations(blocks[0]));
        Assert.Equal([[12L, 10L, 0L]], Locations(blocks[1]));
    }

    [Fact]
    public void Run_ATeleporterToAnotherMap_NamesTheMap()
    {
        Write(Entry("Malas", 10, 10, 0, "Tokuno", 50, 60, 5));

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Equal("Tokuno", ((TomlTable)Assert.Single(Read("malas"))["props"])["map_dest"]);
        Assert.False(File.Exists(Path.Combine(Destination, "tokuno", "teleporters.toml")));
    }

    [Fact]
    public void Run_Back_AddsTheReturnTeleporterOnTheDestinationsMap()
    {
        Write(Entry("Malas", 10, 10, 0, "Tokuno", 50, 60, 5, true));

        Assert.True(Run() == 0, CombinedOutput);

        var back = Assert.Single(Read("tokuno"));
        var props = (TomlTable)back["props"];
        Assert.Equal([10L, 10L, 0L], ((TomlArray)props["point_dest"]).Cast<long>());
        Assert.Equal("Malas", props["map_dest"]);
        Assert.Equal([[50L, 60L, 5L]], Locations(back));
    }

    [Theory]
    // As ModernUO's [TelGen: a later teleporter replaces one on the same cell within 12 of height.
    [InlineData(12, 1)]
    [InlineData(-12, 1)]
    [InlineData(13, 2)]
    public void Run_ALaterTeleporterOnTheSameSpot_ReplacesTheEarlierOne(int height, int expected)
    {
        Write(
            Entry("Trammel", 10, 10, 0, "Trammel", 50, 60, 5),
            Entry("Trammel", 10, 10, height, "Trammel", 70, 80, 0)
        );

        Assert.True(Run() == 0, CombinedOutput);

        var blocks = Read("trammel");
        Assert.Equal(expected, blocks.Count);
        Assert.Equal([70L, 80L, 0L], ((TomlArray)((TomlTable)blocks[^1]["props"])["point_dest"]).Cast<long>());
    }

    [Fact]
    public void Run_Again_ReplacesTheFilesAndRemovesThoseLeftEmpty()
    {
        Write(Entry("Trammel", 10, 10, 0, "Trammel", 50, 60, 5), Entry("Malas", 1, 1, 0, "Malas", 2, 2, 0));
        Assert.True(Run() == 0, CombinedOutput);
        Write(Entry("Trammel", 20, 20, 0, "Trammel", 50, 60, 5));

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Equal([[20L, 20L, 0L]], Locations(Assert.Single(Read("trammel"))));
        Assert.False(File.Exists(Path.Combine(Destination, "malas", "teleporters.toml")));
    }

    [Theory]
    [InlineData(
        """[{ "src": { "map": "Atlantis", "loc": [1, 2, 3] }, "dst": { "map": "Trammel", "loc": [1, 2, 3] }, "back": false }]""",
        "entry 1"
    )]
    [InlineData(
        """[{ "src": { "map": "Trammel", "loc": [1, 2] }, "dst": { "map": "Trammel", "loc": [1, 2, 3] }, "back": false }]""",
        "entry 1"
    )]
    [InlineData("""[{ "src": { "map": "Trammel", "loc": [1, 2, 3] }, "back": false }]""", "entry 1")]
    [InlineData(
        """[{ "src": { "map": "Trammel", "loc": [1, 2, 3] }, "dst": { "map": "Trammel", "loc": [1, 2, 3] }, "back": "true" }]""",
        "entry 1"
    )]
    [InlineData(
        """[{ "src": { "map": "Trammel", "loc": [1, "2", 3] }, "dst": { "map": "Trammel", "loc": [1, 2, 3] }, "back": false }]""",
        "entry 1"
    )]
    [InlineData("[]", "no teleporters")]
    [InlineData("this is not json", "not valid JSON")]
    public void Run_ABadFile_FailsNamingTheProblem_AndWritesNothing(string json, string expected)
    {
        File.WriteAllText(Source, json);

        Assert.Equal(2, Run());

        Assert.Contains(expected, _error.ToString());
        Assert.False(Directory.Exists(Destination));
    }

    [Fact]
    public void Run_ABadEntryAfterGoodOnes_KeepsTheFilesOfAnEarlierRun()
    {
        Write(Entry("Trammel", 10, 10, 0, "Trammel", 50, 60, 5));
        Assert.True(Run() == 0, CombinedOutput);
        Write(Entry("Trammel", 20, 20, 0, "Trammel", 50, 60, 5), Entry("Atlantis", 1, 1, 0, "Trammel", 2, 2, 0));

        Assert.Equal(2, Run());

        Assert.Contains("entry 2", _error.ToString());
        Assert.Equal([[10L, 10L, 0L]], Locations(Assert.Single(Read("trammel"))));
    }

    [Fact]
    public void Run_CommentsAndTrailingCommas_AreRead_AsModernUoReadsThem()
    {
        File.WriteAllText(Source, "[\n// the first\n" + Entry("Trammel", 10, 10, 0, "Trammel", 50, 60, 5) + ",\n]");

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Single(Read("trammel"));
    }

    [Fact]
    public void Run_BackOnASpotThatHasATeleporter_ReplacesIt()
    {
        Write(
            Entry("Trammel", 50, 60, 5, "Trammel", 1, 1, 0),
            Entry("Trammel", 10, 10, 0, "Trammel", 50, 60, 5, true)
        );

        Assert.True(Run() == 0, CombinedOutput);

        var blocks = Read("trammel");
        Assert.Equal(2, blocks.Count);
        var back = Assert.Single(blocks, block => Locations(block)[0][0] == 50L);
        Assert.Equal([10L, 10L, 0L], ((TomlArray)((TomlTable)back["props"])["point_dest"]).Cast<long>());
    }

    [Fact]
    public void Run_AMissingFile_Fails()
    {
        Assert.Equal(2, Run());

        Assert.Contains("does not exist", _error.ToString());
    }

    private static string Entry(string map, int x, int y, int z, string destMap, int dx, int dy, int dz, bool back = false)
    {
        return
            $$"""{ "src": { "map": "{{map}}", "loc": [{{x}}, {{y}}, {{z}}] }, "dst": { "map": "{{destMap}}", "loc": [{{dx}}, {{dy}}, {{dz}}] }, "back": {{(back ? "true" : "false")}} }""";
    }

    private static List<List<long>> Locations(TomlTable block)
    {
        return ((TomlArray)block["locations"]).Select(location => ((TomlArray)location!).Cast<long>().ToList()).ToList();
    }

    private void Write(params string[] entries)
    {
        File.WriteAllText(Source, "[\n" + string.Join(",\n", entries) + "\n]");
    }

    private int Run()
    {
        return ModernUoTeleporterConverter.Run(Source, Destination, _output, _error);
    }

    private List<TomlTable> Read(string folder)
    {
        var document =
            TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(Path.Combine(Destination, folder, "teleporters.toml")))!;

        return ((TomlTableArray)document["decoration"]).ToList();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
