using System.Net;
using DryIoc;
using Moongate.Http.Plugin.Endpoints;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Http.Plugin.Services;
using Moongate.Tests.Support;
using Moongate.Ultima.Catalog;
using Moongate.Ultima.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Tests.Http;

[Collection("UltimaClientData")]
public class ItemImageEndpointsTests
{
    private static async Task<TestHttpServer> StartAsync(ItemImageFixture fixture)
        => await TestHttpServer.StartAsync(
            container =>
            {
                container.RegisterInstance(fixture.Directories);
                container.Register<IItemCatalog, ItemCatalog>(Reuse.Singleton);
                container.Register<IItemImageService, ItemImageService>(Reuse.Singleton);
                container.RegisterApiEndpointInstance(
                    new ItemImageEndpoints(container.Resolve<IItemImageService>())
                );
            }
        );

    [Fact]
    public async Task Get_KnownItem_ReturnsADecodablePng()
    {
        using var fixture = ItemImageFixture.Create();
        await using var server = await StartAsync(fixture);

        var response = await server.Client.GetAsync($"/api/v1/images/items/0x{ItemImageFixture.ItemId:x4}.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);

        using var image = Image.Load<Bgra32>(await response.Content.ReadAsByteArrayAsync());

        Assert.Equal(2, image.Width);
    }

    [Fact]
    public async Task Get_NoToken_StillServesTheImage()
    {
        // Anonymous on purpose: the art is client data every player already has on disk.
        using var fixture = ItemImageFixture.Create();
        await using var server = await StartAsync(fixture);

        var response = await server.Client.GetAsync($"/api/v1/images/items/0x{ItemImageFixture.ItemId:x4}.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_IdWithoutThePrefix_IsReadAsHex()
    {
        using var fixture = ItemImageFixture.Create();
        await using var server = await StartAsync(fixture);

        var response = await server.Client.GetAsync($"/api/v1/images/items/{ItemImageFixture.ItemId:x4}.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_Hued_ReturnsDifferentPixelsFromThePlainArt()
    {
        using var fixture = ItemImageFixture.Create();
        await using var server = await StartAsync(fixture);

        var plain = await server.Client.GetByteArrayAsync(
            $"/api/v1/images/items/0x{ItemImageFixture.ItemId:x4}.png"
        );
        var hued = await server.Client.GetByteArrayAsync(
            $"/api/v1/images/items/0x{ItemImageFixture.ItemId:x4}.png?hue=0x{ItemImageFixture.Hue:x4}"
        );

        using var plainImage = Image.Load<Bgra32>(plain);
        using var huedImage = Image.Load<Bgra32>(hued);

        Assert.NotEqual(plainImage[0, 0], huedImage[0, 0]);
    }

    [Fact]
    public async Task Get_ItemWithoutArt_IsNotFound()
    {
        using var fixture = ItemImageFixture.Create();
        await using var server = await StartAsync(fixture);

        var response = await server.Client.GetAsync(
            $"/api/v1/images/items/0x{ItemImageFixture.MissingArtItemId:x4}.png"
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_MalformedId_IsBadRequest()
    {
        using var fixture = ItemImageFixture.Create();
        await using var server = await StartAsync(fixture);

        var response = await server.Client.GetAsync("/api/v1/images/items/zzzz.png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_HueOutOfRange_IsBadRequest()
    {
        // Hues.GetHue never fails: it masks the index and falls back to hue 0. Without this check the
        // request would answer 200 with the wrong image.
        using var fixture = ItemImageFixture.Create();
        await using var server = await StartAsync(fixture);

        var response = await server.Client.GetAsync(
            $"/api/v1/images/items/0x{ItemImageFixture.ItemId:x4}.png?hue=0x9999"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
