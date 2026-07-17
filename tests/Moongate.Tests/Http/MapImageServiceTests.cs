using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Services;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Tests.Http;

[Collection("UltimaClientData")]
public class MapImageServiceTests
{
    private static MapImageService Service(MapImageFixture fixture)
        => new(fixture.Provider, fixture.Directories, new UltimaReadGate());

    [Fact]
    public void MaxZoomFor_FollowsTheGeometry()
    {
        using var fixture = MapImageFixture.Create();

        Assert.Equal(
            MapTileGeometry.MaxZoom(MapImageFixture.MapWidth, MapImageFixture.MapHeight),
            Service(fixture).MaxZoomFor(MapType.Felucca)
        );
    }

    [Fact]
    public async Task GetTileAsync_Native_RendersA256Tile()
    {
        using var fixture = MapImageFixture.Create();
        var service = Service(fixture);

        var path = await service.GetTileAsync(MapType.Felucca, service.MaxZoomFor(MapType.Felucca), 0, 0);

        Assert.NotNull(path);

        using var image = await Image.LoadAsync<Bgra32>(path!);

        Assert.Equal(MapTileGeometry.TileSize, image.Width);
        Assert.Equal(MapTileGeometry.TileSize, image.Height);
    }

    [Fact]
    public async Task GetTileAsync_SecondCall_ServesTheCachedFileWithoutRerendering()
    {
        using var fixture = MapImageFixture.Create();
        var service = Service(fixture);
        var zoom = service.MaxZoomFor(MapType.Felucca);

        var first = await service.GetTileAsync(MapType.Felucca, zoom, 0, 0);
        var marker = DateTime.UtcNow.AddDays(-1);
        File.SetLastWriteTimeUtc(first!, marker);

        var second = await service.GetTileAsync(MapType.Felucca, zoom, 0, 0);

        Assert.Equal(first, second);
        Assert.Equal(marker, File.GetLastWriteTimeUtc(second!), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetTileAsync_ZoomZero_ComposesFromChildrenIncludingTheOnesThatDoNotExist()
    {
        // The case that matters. This facet's z1 grid is 2x1, so the single z0 tile asks for (0,1) and
        // (1,1), which are outside their own level's grid. A composer that treats a missing child as a
        // failure makes z0 unreachable on every non-square facet — which is all of them.
        using var fixture = MapImageFixture.Create();
        var service = Service(fixture);

        var path = await service.GetTileAsync(MapType.Felucca, 0, 0, 0);

        Assert.NotNull(path);

        using var image = await Image.LoadAsync<Bgra32>(path!);

        Assert.Equal(MapTileGeometry.TileSize, image.Width);
    }

    [Fact]
    public async Task GetTileAsync_ZoomZero_LeavesItsChildrenCachedOnTheWay()
    {
        // Proof the pyramid is built bottom-up out of cached tiles rather than by rendering the whole
        // facet into one bitmap, which for Felucca would be about 100 MB.
        using var fixture = MapImageFixture.Create();
        var service = Service(fixture);

        await service.GetTileAsync(MapType.Felucca, 0, 0, 0);

        var native = service.MaxZoomFor(MapType.Felucca);
        var root = fixture.Directories.GetPath("cache/images/maps");

        Assert.True(Directory.Exists(Path.Combine(root, "felucca", native.ToString())), "no native tiles cached");
        Assert.True(Directory.Exists(Path.Combine(root, "felucca", "0")), "no zoom 0 tile cached");
    }

    [Fact]
    public async Task GetTileAsync_OutsideTheGrid_ReturnsNull()
    {
        using var fixture = MapImageFixture.Create();

        Assert.Null(await Service(fixture).GetTileAsync(MapType.Felucca, 0, 99, 99));
    }

    [Fact]
    public async Task GetTileAsync_UnknownFacet_ReturnsNull()
    {
        using var fixture = MapImageFixture.Create();

        Assert.Null(await Service(fixture).GetTileAsync(MapType.Malas, 0, 0, 0));
    }

    [Fact]
    public async Task GetFullAsync_IsTheWholeFacet()
    {
        using var fixture = MapImageFixture.Create();

        var path = await Service(fixture).GetFullAsync(MapType.Felucca);

        Assert.NotNull(path);

        using var image = await Image.LoadAsync<Bgra32>(path!);

        Assert.Equal(MapImageFixture.MapWidth, image.Width);
        Assert.Equal(MapImageFixture.MapHeight, image.Height);
    }

    [Fact]
    public async Task GetFullAsync_SecondCall_ServesTheCachedFile()
    {
        using var fixture = MapImageFixture.Create();
        var service = Service(fixture);

        var first = await service.GetFullAsync(MapType.Felucca);
        var marker = DateTime.UtcNow.AddDays(-1);
        File.SetLastWriteTimeUtc(first!, marker);

        var second = await service.GetFullAsync(MapType.Felucca);

        Assert.Equal(marker, File.GetLastWriteTimeUtc(second!), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetTileAsync_ConcurrentCallers_AllSucceed()
    {
        // Map.GetImage descends into Art, which holds no lock and shares a scratch buffer and a file
        // stream position. Without the gate these corrupt each other.
        using var fixture = MapImageFixture.Create();
        var service = Service(fixture);
        var zoom = service.MaxZoomFor(MapType.Felucca);

        var paths = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(x => service.GetTileAsync(MapType.Felucca, zoom, x, 0))
        );

        Assert.All(paths, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public void IsReady_TrueOnceTheClientFilesAreLoaded()
    {
        using var fixture = MapImageFixture.Create();

        Assert.True(Service(fixture).IsReady);
    }
}
