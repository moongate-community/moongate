using System.Net;
using System.Net.Http.Json;
using DryIoc;
using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Endpoints.Maps;
using Moongate.Http.Plugin.Interfaces.Maps;
using Moongate.Http.Plugin.Interfaces.Ultima;
using Moongate.Http.Plugin.Services.Maps;
using Moongate.Http.Plugin.Services.Ultima;
using Moongate.Tests.Support;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Tests.Http.Endpoints.Maps;

[Collection("UltimaClientData")]
public class MapImageEndpointsTests
{
    private static async Task<TestHttpServer> StartAsync(MapImageFixture fixture)
        => await TestHttpServer.StartAsync(
            container =>
            {
                container.RegisterInstance(fixture.Directories);
                container.RegisterInstance(fixture.Provider);
                container.Register<IUltimaReadGate, UltimaReadGate>(Reuse.Singleton);
                container.Register<IMapImageService, MapImageService>(Reuse.Singleton);
                container.RegisterApiEndpointInstance(
                    new MapImageEndpoints(
                        container.Resolve<IMapImageService>(),
                        container.Resolve<IUltimaMapProvider>()
                    )
                );
            }
        );

    [Fact]
    public async Task GetTile_NoToken_StillServesIt()
    {
        // Anonymous on purpose: map data is client data every player already has on disk.
        using var fixture = MapImageFixture.Create();
        await using var server = await StartAsync(fixture);

        var response = await server.Client.GetAsync("/api/v1/images/maps/felucca/0/0/0.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);

        using var image = Image.Load<Bgra32>(await response.Content.ReadAsByteArrayAsync());

        Assert.Equal(MapTileGeometry.TileSize, image.Width);
    }

    [Fact]
    public async Task GetTile_FacetNameIsCaseInsensitive()
    {
        using var fixture = MapImageFixture.Create();
        await using var server = await StartAsync(fixture);

        Assert.Equal(
            HttpStatusCode.OK,
            (await server.Client.GetAsync("/api/v1/images/maps/FELUCCA/0/0/0.png")).StatusCode
        );
    }

    [Fact]
    public async Task GetTile_UnknownFacet_IsBadRequest()
    {
        using var fixture = MapImageFixture.Create();
        await using var server = await StartAsync(fixture);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await server.Client.GetAsync("/api/v1/images/maps/atlantis/0/0/0.png")).StatusCode
        );
    }

    [Fact]
    public async Task GetTile_ZoomBeyondNative_IsBadRequest()
    {
        using var fixture = MapImageFixture.Create();
        await using var server = await StartAsync(fixture);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await server.Client.GetAsync("/api/v1/images/maps/felucca/99/0/0.png")).StatusCode
        );
    }

    [Fact]
    public async Task GetTile_OutsideTheGrid_IsNotFound()
    {
        using var fixture = MapImageFixture.Create();
        await using var server = await StartAsync(fixture);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await server.Client.GetAsync("/api/v1/images/maps/felucca/0/9/9.png")).StatusCode
        );
    }

    [Fact]
    public async Task GetFull_ReturnsTheWholeFacet()
    {
        using var fixture = MapImageFixture.Create();
        await using var server = await StartAsync(fixture);

        var response = await server.Client.GetAsync("/api/v1/images/maps/felucca/full.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var image = Image.Load<Bgra32>(await response.Content.ReadAsByteArrayAsync());

        Assert.Equal(MapImageFixture.MapWidth, image.Width);
        Assert.Equal(MapImageFixture.MapHeight, image.Height);
    }

    [Fact]
    public async Task List_DescribesEachFacetSoAViewerNeedNotHardcodeIt()
    {
        using var fixture = MapImageFixture.Create();
        await using var server = await StartAsync(fixture);

        var facets = await server.Client.GetFromJsonAsync<List<MapFacetInfo>>("/api/v1/images/maps");

        var only = Assert.Single(facets!);

        Assert.Equal("Felucca", only.Name);
        Assert.Equal(MapImageFixture.MapWidth, only.Width);
        Assert.Equal(MapTileGeometry.MaxZoom(MapImageFixture.MapWidth, MapImageFixture.MapHeight), only.MaxZoom);
        Assert.Equal(MapTileGeometry.TileSize, only.TileSize);
    }

    [Fact]
    public async Task GetTile_ReliefStyle_ServesAnImage()
    {
        using var fixture = MapImageFixture.Create();
        await using var server = await StartAsync(fixture);

        var response = await server.Client.GetAsync("/api/v1/images/maps/felucca/0/0/0.png?style=relief");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetTile_UnknownStyle_IsABadRequest()
    {
        using var fixture = MapImageFixture.Create();
        await using var server = await StartAsync(fixture);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await server.Client.GetAsync("/api/v1/images/maps/felucca/0/0/0.png?style=bogus")).StatusCode
        );
    }
}
