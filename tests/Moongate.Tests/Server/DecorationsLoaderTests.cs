using Moongate.Server.Loaders;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

/// <summary>
/// Which facet each decoration file belongs to. This is the part of the loader that can be wrong
/// without anything looking wrong: a file mapped to the wrong facet, or to one facet where it belongs
/// on two, still loads cleanly and still reports a plausible count — the shard is just missing a
/// continent nobody walked to.
/// </summary>
public class DecorationsLoaderTests
{
    /// <summary>
    /// Britannia is the shared landmass. Felucca and Trammel are mirrors of the same continent, the
    /// towns stand on both, and their decoration is authored once — which is why RunUO and ModernUO
    /// both generate that folder against the two facets and only the facet-specific folders once.
    /// Loading it onto Felucca alone leaves Trammel with its handful of exclusives and nothing else.
    /// </summary>
    [Fact]
    public async Task LoadAsync_TheSharedBritanniaFile_LandsOnBothMirrorFacets()
    {
        var root = TemporaryDirectory.Create("mg-decor-");
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteFacets(directories, britannia: "- Type: Static\n  ItemId: 100\n  At:\n    - [1, 2, 3]\n");
        var catalog = new DecorationCatalog();

        try
        {
            await new DecorationsLoader(catalog, directories).LoadAsync();

            Assert.Equal([0, 1], catalog.All.Where(p => p.ItemId == 100).Select(p => p.MapId).Order());
        }
        finally
        {
            TemporaryDirectory.Remove(root);
        }
    }

    // The facet-specific files are the counterpart: authored for one map, loaded onto one map.
    [Fact]
    public async Task LoadAsync_AFacetSpecificFile_LandsOnThatFacetOnly()
    {
        var root = TemporaryDirectory.Create("mg-decor-");
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteFacets(directories, trammel: "- Type: Static\n  ItemId: 200\n  At:\n    - [4, 5, 6]\n");
        var catalog = new DecorationCatalog();

        try
        {
            await new DecorationsLoader(catalog, directories).LoadAsync();

            Assert.Equal(1, Assert.Single(catalog.All, p => p.ItemId == 200).MapId);
        }
        finally
        {
            TemporaryDirectory.Remove(root);
        }
    }

    /// <summary>
    /// Writes every facet file so the loader parses these instead of seeding the real corpus — which
    /// would make the assertions above depend on forty thousand shipped objects.
    /// </summary>
    private static void WriteFacets(DirectoriesConfig directories, string? britannia = null, string? trammel = null)
    {
        var directory = Path.Combine(directories.RegisterDirectory("data"), "decorations");
        Directory.CreateDirectory(directory);

        string[] facets =
        [
            "britannia", "felucca", "trammel", "ilshenar", "malas", "tokuno", "ruinedmaginciafel",
            "ruinedmaginciatram"
        ];

        foreach (var facet in facets)
        {
            var content = facet switch
            {
                "britannia" => britannia,
                "trammel"   => trammel,
                _           => null
            };

            File.WriteAllText(Path.Combine(directory, facet + ".yaml"), content ?? "[]\n");
        }
    }
}
