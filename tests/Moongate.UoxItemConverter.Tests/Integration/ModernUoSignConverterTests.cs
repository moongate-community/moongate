using Moongate.UoxItemConverter.Internal;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.UoxItemConverter.Tests.Integration;

public sealed class ModernUoSignConverterTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "moongate-modernuo-signs-" + Guid.NewGuid().ToString("N")
    );

    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string Source => Path.Combine(_root, "signs.cfg");

    private string Destination => Path.Combine(_root, "decorations");

    private string CombinedOutput => _output + _error.ToString();

    public ModernUoSignConverterTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Run_SignsOfEveryFacet_GoToTheFolderOfTheirMaps()
    {
        File.WriteAllLines(
            Source,
            [
                "0 3032 373 904 -1 #1016093",
                "1 2996 10 20 0 #1016315",
                "2 2979 3632 2537 0 The Shakin' Bakery",
                "3 3016 30 40 5 #1016095",
                "4 3026 50 60 0 #1016082",
                "5 3025 70 80 4 #1016216"
            ]
        );

        Assert.True(Run() == 0, CombinedOutput);

        var britannia = Assert.Single(Read("britannia"));
        Assert.Equal("LocalizedSign", britannia["type"]);
        Assert.Equal(3032L, britannia["item_id"]);
        Assert.Equal(1016093L, ((TomlTable)britannia["props"])["label_number"]);
        Assert.Equal([373L, 904L, -1L], ((TomlArray)((TomlArray)britannia["locations"])[0]!).Cast<long>());

        // The signs of Trammel alone are those of the old Haven, a ruin on the client's map: written set aside.
        Assert.False(File.Exists(Path.Combine(Destination, "trammel", "signs.toml")));
        var trammel = Assert.Single(Read("trammel"));
        Assert.Equal("Sign", trammel["type"]);
        Assert.Equal("The Shakin' Bakery", ((TomlTable)trammel["props"])["name"]);

        Assert.Single(Read("felucca"));
        Assert.Single(Read("ilshenar"));
        Assert.Single(Read("malas"));
        Assert.Single(Read("tokuno"));
    }

    [Fact]
    public void Run_SignsWithTheSameGraphicAndText_ShareOneBlock()
    {
        File.WriteAllLines(Source, ["0 3032 1 2 3 #1016093", "0 3032 4 5 6 #1016093", "0 3033 7 8 9 #1016093"]);

        Assert.True(Run() == 0, CombinedOutput);

        var blocks = Read("britannia");
        Assert.Equal(2, blocks.Count);
        Assert.Equal(2, ((TomlArray)blocks[0]["locations"]).Count);
    }

    [Fact]
    public void Run_ASignInTheMalasTowns_GetsTheHueOfItsTown()
    {
        File.WriteAllLines(Source, ["4 3026 970 510 0 #1", "4 3026 1960 1278 0 #1", "4 3026 10 10 0 #1"]);

        Assert.True(Run() == 0, CombinedOutput);

        var blocks = Read("malas");
        Assert.Equal(3, blocks.Count);
        Assert.Equal(0x47EL, ((TomlTable)blocks[0]["props"])["hue"]);
        Assert.Equal(0x44EL, ((TomlTable)blocks[1]["props"])["hue"]);
        Assert.False(((TomlTable)blocks[2]["props"]).ContainsKey("hue"));
    }

    [Fact]
    public void Run_ANameWithQuotes_IsEscaped()
    {
        File.WriteAllLines(Source, ["0 3032 1 2 3 The \"Best\" \\ Inn"]);

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Equal("The \"Best\" \\ Inn", ((TomlTable)Assert.Single(Read("britannia"))["props"])["name"]);
    }

    [Fact]
    public void Run_AMissingSource_Fails()
    {
        Assert.Equal(2, Run());
        Assert.Contains("signs.cfg", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ALineThatIsNotASign_FailsNamingIt()
    {
        File.WriteAllLines(Source, ["0 3032 1 2 3 #1", "9 3032 1 2 3 #1"]);

        Assert.Equal(2, Run());
        Assert.Contains("line 2", _error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Destination));
    }

    private int Run()
    {
        return ModernUoSignConverter.Run(Source, Destination, _output, _error);
    }

    private List<TomlTable> Read(string folder)
    {
        var document = TomlSerializer.Deserialize<TomlTable>(
            File.ReadAllText(Path.Combine(Destination, folder, folder == "trammel" ? "_signs.toml" : "signs.toml"))
        )!;

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
