using Moongate.Core.Utils;
using Moongate.Server.Ultima.Types.Decorations;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.Tests.Server.Ultima.Loaders;

/// <summary>
///     Reads the decoration files converted from ModernUO's Data/Decoration (and ServUO's New Haven) into
///     <c>moongate_root/templates/decorations</c>,
///     so a broken file fails here instead of when the world is decorated.
/// </summary>
public sealed class RepositoryDecorationFilesTests
{
    private static readonly string[] Folders =
    [
        "britannia", "trammel", "felucca", "ilshenar", "malas", "tokuno", "termur", "_ruined_magincia_tram", "_ruined_magincia_fel",
        "_old_magincia", "_bounty_boards"
    ];

    [Fact]
    public void ShippedDecorations_HoldEveryPositionOfTheSourceInTheExpectedFolders()
    {
        var root = DecorationsRoot();

        Assert.Equal(Folders.Order(), Directory.GetDirectories(root).Select(Path.GetFileName).Order());
        Assert.Equal(115, Directory.GetFiles(root, "*.toml", SearchOption.AllDirectories).Length);
        Assert.Equal(42642, Blocks().Sum(block => ((TomlArray)block["locations"]).Count));
    }

    [Fact]
    public void EveryBlock_HasATypeAGraphicOrIsAnAddon_AndWholeLocations()
    {
        foreach (var block in Blocks())
        {
            var type = Assert.IsType<string>(block["type"]);
            Assert.False(string.IsNullOrWhiteSpace(type));
            Assert.True(block.ContainsKey("item_id") || type.EndsWith("Addon", StringComparison.Ordinal), type);

            var locations = Assert.IsType<TomlArray>(block["locations"]);
            Assert.NotEmpty(locations);
            Assert.All(locations, location => Assert.Equal(3, Assert.IsType<TomlArray>(location).OfType<long>().Count()));

            if (block.TryGetValue("extras", out var extras))
            {
                Assert.Equal(locations.Count, ((TomlArray)extras).Count);
            }
        }
    }

    [Fact]
    public void EveryDoorFacing_IsADoorFacingType()
    {
        var facings = Blocks()
                      .Where(block => block.TryGetValue("props", out var props) && ((TomlTable)props).ContainsKey("facing"))
                      .Select(block => (string)((TomlTable)block["props"])["facing"])
                      .ToList();

        Assert.Equal(355, facings.Count);
        Assert.All(facings, facing => Assert.True(EnumNameUtils.TryParse<DoorFacingType>(facing, out _), facing));
        Assert.True(EnumNameUtils.TryParse<DoorFacingType>("west_cw", out var westCw) && westCw == DoorFacingType.WestCW);
    }

    private static IEnumerable<TomlTable> Blocks()
    {
        foreach (var file in Directory.GetFiles(DecorationsRoot(), "*.toml", SearchOption.AllDirectories))
        {
            var document = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(file))!;

            foreach (var block in (TomlTableArray)document["decoration"])
            {
                yield return block;
            }
        }
    }

    private static string DecorationsRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "moongate_root", "templates", "decorations");
    }
}
