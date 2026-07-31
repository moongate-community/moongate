using System.Net;
using System.Net.Http.Headers;
using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Http.Plugin.Endpoints.Mobiles;
using Moongate.Http.Plugin.Extensions;
using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http.Endpoints.Mobiles;

/// <summary>
/// What the route owns is the HTTP conversation, not the picture: a 404 for nobody, an ETag that
/// is the appearance fingerprint, and a 304 when the caller already has that appearance. The 304
/// is what lets the URL stay stable while the picture changes.
/// </summary>
public class MobileCharacterImageEndpointsTests
{
    [Fact]
    public async Task Get_AnUnknownSerial_Is404()
    {
        await using var server = await StartAsync(new StubImageService(null));

        var response = await server.Client.GetAsync("/api/v1/images/mobiles/57005.png");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_AKnownCharacter_ReturnsPngWithTheFingerprintAsETag()
    {
        var image = Png("abc12345");
        await using var server = await StartAsync(new StubImageService(image));

        var response = await server.Client.GetAsync("/api/v1/images/mobiles/1.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("\"abc12345\"", response.Headers.ETag?.ToString());
    }

    // The whole reason the URL does not carry the hash: an unchanged character costs a string
    // comparison rather than a render or a download.
    [Fact]
    public async Task Get_WithAMatchingIfNoneMatch_Is304WithNoBody()
    {
        var image = Png("abc12345");
        await using var server = await StartAsync(new StubImageService(image));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/images/mobiles/1.png");

        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"abc12345\""));

        var response = await server.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    // A stale fingerprint is exactly the case the design exists for: the character changed, so the
    // caller must be given the new picture rather than a 304.
    [Fact]
    public async Task Get_WithAStaleIfNoneMatch_ReturnsTheNewImage()
    {
        var image = Png("newhash1");
        await using var server = await StartAsync(new StubImageService(image));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/images/mobiles/1.png");

        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"oldhash0\""));

        var response = await server.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task GetPaperdoll_ForAKnownCharacter_ReturnsPng()
    {
        var image = Png("dollhash");
        await using var server = await StartAsync(new StubImageService(image));

        var response = await server.Client.GetAsync("/api/v1/images/mobiles/1/paperdoll.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"dollhash\"", response.Headers.ETag?.ToString());
    }

    private static MobileImage Png(string hash)
    {
        var path = Path.Combine(TemporaryDirectory.Create("mg-character-route-"), $"{hash}.png");

        File.WriteAllBytes(path, [0x89, (byte)'P', (byte)'N', (byte)'G']);

        return new(path, hash);
    }

    private static async Task<TestApiServer> StartAsync(StubImageService images)
        => await TestApiServer.StartAsync(
               configure: container =>
                          {
                              container.RegisterInstance<IMobileCharacterImageService>(images);
                              container.RegisterApiEndpointInstance(new MobileCharacterImageEndpoints(images));
                          }
           );

    private sealed class StubImageService : IMobileCharacterImageService
    {
        private readonly MobileImage? _image;

        public StubImageService(MobileImage? image)
        {
            _image = image;
        }

        public Task<MobileImage?> GetFigureAsync(Serial serial, CancellationToken cancellationToken = default)
            => Task.FromResult(_image);

        public Task<MobileImage?> GetPaperdollAsync(
            Serial serial,
            bool includeBackground,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(_image);
    }
}
