using Moongate.Ultima.Graphics;
using Moongate.Ultima.Io;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Support;

/// <summary>
/// A synthetic UO client directory holding one item's art, plus a throwaway runtime root for the image
/// cache. Building it mutates Moongate.Ultima's process-wide statics, so every test using it must sit in
/// the "UltimaClientData" collection.
/// </summary>
public sealed class ItemImageFixture : IDisposable
{
    /// <summary>The only id with art. Any other id exercises the missing-art path.</summary>
    public const int ItemId = 0x10;

    /// <summary>An id the art file does not cover, for the 404 path.</summary>
    public const int MissingArtItemId = 0x20;

    /// <summary>
    /// The one hue the synthetic hues.mul actually colours. Hues.Initialize fills every remaining index up
    /// to 3000 with a default, so higher hues are valid but paint nothing.
    /// </summary>
    public const ushort Hue = 1;

    private readonly string _clientDirectory;

    private ItemImageFixture(string clientDirectory, string root, DirectoriesConfig directories)
    {
        _clientDirectory = clientDirectory;
        Root = root;
        Directories = directories;
    }

    public string Root { get; }

    public DirectoriesConfig Directories { get; }

    public static ItemImageFixture Create()
    {
        var tileData = UltimaFixtures.BuildTileData();

        UltimaFixtures.SetItem(
            tileData,
            ItemId,
            (uint)(TileFlagType.Wearable | TileFlagType.PartialHue),
            4,
            "test tunic"
        );

        var (artIndex, art) = UltimaFixtures.BuildStaticArt(ItemId, 2, 2, 0xFFFF); // white -> gray, hueable
        var hues = UltimaFixtures.BuildHues("Red", 0x7C00, 0, 0);

        var clientDirectory = UltimaFixtures.CreateClientDirectory(
            ("tiledata.mul", tileData),
            ("artidx.mul", artIndex),
            ("art.mul", art),
            ("hues.mul", hues)
        );

        Files.SetDirectory(clientDirectory);
        Art.Reload();
        TileData.Initialize();
        Hues.Initialize();

        var root = Path.Combine(Path.GetTempPath(), $"mg_images_{Guid.NewGuid():N}");

        return new(clientDirectory, root, new(root, []));
    }

    public void Dispose()
    {
        if (Directory.Exists(_clientDirectory))
        {
            Directory.Delete(_clientDirectory, true);
        }

        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }
    }
}
