using System.Net;
using DryIoc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Http.Plugin.Services;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http;

public class HttpServerServiceTests
{
    private sealed class ProbeEndpoints : IApiEndpointRegistration
    {
        public void Register(IEndpointRouteBuilder routes)
            => routes.MapGet("/probe", () => Results.Ok("mapped"));
    }

    [Fact]
    public async Task StartAsync_MapsRegisteredEndpointGroups()
    {
        await using var server = await TestHttpServer.StartAsync(
            container => container.RegisterApiEndpointInstance(new ProbeEndpoints())
        );

        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/probe")).StatusCode);
    }

    [Fact]
    public async Task StartAsync_ServesHealth()
    {
        await using var server = await TestHttpServer.StartAsync();

        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task StartAsync_ServesTheOpenApiDocumentAndScalar()
    {
        await using var server = await TestHttpServer.StartAsync();

        Assert.Equal(
            HttpStatusCode.OK,
            (await server.Client.GetAsync(HttpServerService.SwaggerDocumentRoute)).StatusCode
        );
        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/scalar/v1")).StatusCode);
    }

    [Fact]
    public async Task StartAsync_MissingSigningKey_FailsLoudly()
    {
        // A key generated per restart would invalidate every token on every restart and read as a
        // random bug. Refusing to start says so plainly instead.
        var container = new Container();
        var config = new MoongateHttpConfig { Port = 0, Jwt = new() { SigningKey = string.Empty } };
        container.RegisterInstance(config);

        var service = new HttpServerService(container, config);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.StartAsync());
        Assert.Contains("SigningKey", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StopAsync_ReleasesThePort()
    {
        var server = await TestHttpServer.StartAsync();
        var port = server.Port;
        await server.DisposeAsync();

        // Binding the same port again proves the listener really let go.
        await using var second = await TestHttpServer.StartAsync(port: port);

        Assert.Equal(HttpStatusCode.OK, (await second.Client.GetAsync("/health")).StatusCode);
    }
}
