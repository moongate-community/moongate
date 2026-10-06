using Moongate.UoxItemConverter.Internal;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.UoxItemConverter.Tests.Integration;

public sealed class ModernUoLocationConverterTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "moongate-modernuo-locations-" + Guid.NewGuid().ToString("N")
    );

    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string Source => Path.Combine(_root, "Locations");

    private string Destination => Path.Combine(_root, "data", "locations.toml");

    private string CombinedOutput => _output + _error.ToString();

    public ModernUoLocationConverterTests()
    {
        Directory.CreateDirectory(Source);
    }

    [Fact]
    public void Run_APlaceInNestedCategories_KeepsItsMapItsCategoryPathAndItsSpot()
    {
        Write(
            "felucca",
            """
            { "name": "Felucca", "categories": [
              { "name": "Dungeons", "categories": [
                { "name": "Covetous", "locations": [
                  { "name": "Entrance", "location": [2499, 919, 0] },
                  { "name": "Level 1", "location": [5456, 1863, 0] } ] } ] } ] }
            """
        );

        Assert.True(Run() == 0, CombinedOutput);

        var places = Read();
        Assert.Equal(2, places.Count);
        Assert.Equal(("felucca", "Dungeons/Covetous", "Entrance", "(2499, 919, 0)"), Row(places[0]));
        Assert.Equal(("felucca", "Dungeons/Covetous", "Level 1", "(5456, 1863, 0)"), Row(places[1]));
        Assert.Contains("2 places on 1 maps", _output.ToString());
    }

    [Fact]
    public void Run_PlacesBesideCategories_AndAtTheTopOfAMap_AreKept()
    {
        Write(
            "malas",
            """
            { "name": "Malas", "locations": [ { "name": "Arena", "location": [1, 2, -3] } ],
              "categories": [ { "name": "Towns",
                "locations": [ { "name": "Luna", "location": [989, 520, -50] } ],
                "categories": [ { "name": "Inns", "locations": [ { "name": "Luna Inn", "location": [4, 5, 6] } ] } ] } ] }
            """
        );

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Equal(
            [
                ("malas", "", "Arena", "(1, 2, -3)"), ("malas", "Towns", "Luna", "(989, 520, -50)"),
                ("malas", "Towns/Inns", "Luna Inn", "(4, 5, 6)")
            ],
            Read().Select(Row)
        );
    }

    [Fact]
    public void Run_EveryMapFile_GoesIntoTheOneFile_InTheOrderOfTheMaps()
    {
        foreach (var map in new[] { "trammel", "felucca", "termur", "tokuno", "malas", "ilshenar" })
        {
            Write(map, """{ "name": "X", "locations": [ { "name": "Here", "location": [1, 2, 3] } ] }""");
        }

        Assert.True(Run() == 0, CombinedOutput);

        Assert.Equal(
            ["felucca", "trammel", "ilshenar", "malas", "tokuno", "termur"],
            Read().Select(place => (string)place["map"])
        );
    }

    [Fact]
    public void Run_ANameWithAQuoteOrASlash_IsWrittenSoItReadsBack()
    {
        Write(
            "felucca",
            """{ "name": "F", "categories": [ { "name": "East/West", "locations": [ { "name": "Buccaneer's \"Den\"", "location": [1, 2, 3] } ] } ] }"""
        );

        Assert.True(Run() == 0, CombinedOutput);

        var place = Assert.Single(Read());
        // A slash would split the category in two.
        Assert.Equal(("East-West", "Buccaneer's \"Den\""), ((string)place["category"], (string)place["name"]));
    }

    [Theory]
    [InlineData("""{ "name": "F", "locations": [ { "name": "Here", "location": [1, 2] } ] }""", "Here")]
    [InlineData("""{ "name": "F", "locations": [ { "location": [1, 2, 3] } ] }""", "felucca.json")]
    [InlineData("""{ "name": "F", "locations": [ { "name": "Here", "location": [1, "2", 3] } ] }""", "Here")]
    [InlineData("this is not json", "felucca.json")]
    public void Run_ABadFile_FailsNamingIt_AndKeepsTheFileOfAnEarlierRun(string json, string expected)
    {
        Write("felucca", """{ "name": "F", "locations": [ { "name": "Kept", "location": [1, 2, 3] } ] }""");
        Assert.True(Run() == 0, CombinedOutput);
        Write("felucca", json);

        Assert.Equal(2, Run());

        Assert.Contains(expected, _error.ToString());
        Assert.Equal("Kept", Assert.Single(Read())["name"]);
    }

    [Fact]
    public void Run_AFolderWithNoMapFile_Fails()
    {
        Assert.Equal(2, Run());

        Assert.Contains("no places", _error.ToString());
        Assert.False(File.Exists(Destination));
    }

    [Fact]
    public void Run_AMissingFolder_Fails()
    {
        Directory.Delete(Source);

        Assert.Equal(2, Run());

        Assert.Contains("does not exist", _error.ToString());
    }

    private static (string, string, string, string) Row(TomlTable place)
    {
        return ((string)place["map"], (string)place["category"], (string)place["name"], (string)place["location"]);
    }

    private void Write(string map, string json)
    {
        File.WriteAllText(Path.Combine(Source, map + ".json"), json);
    }

    private int Run()
    {
        return ModernUoLocationConverter.Run(Source, Destination, _output, _error);
    }

    private List<TomlTable> Read()
    {
        var document = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(Destination))!;

        return ((TomlTableArray)document["location"]).ToList();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
