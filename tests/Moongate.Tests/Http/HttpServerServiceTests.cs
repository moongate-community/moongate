using System.Net;
using DryIoc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Interfaces;
using Moongate.Http.Plugin.Services;
using Moongate.Tests.Support;
using SquidStd.Core.Interfaces.Config;

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
    public async Task StartAsync_ConfiguredSigningKeyTooShort_FailsLoudly()
    {
        // Set but unusable: a mistake, not an omission. Starting anyway would mint tokens nobody can
        // verify, so this is worth stopping for rather than papering over with a generated key.
        var container = new Container();
        var config = new MoongateHttpConfig { Port = 0, Jwt = new() { SigningKey = "too-short-for-hs256" } };
        container.RegisterInstance(config);

        var service = new HttpServerService(container, config);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.StartAsync());
        Assert.Contains("SigningKey", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsync_NoSigningKeyConfigured_MintsOneAndPersistsIt()
    {
        // Persisting is the point: a key minted per boot would invalidate every token issued before the
        // restart, and nothing would say so.
        var container = new Container();
        var config = new MoongateHttpConfig { Address = "127.0.0.1", Port = 0, Jwt = new() { SigningKey = string.Empty } };
        var configManager = new StubConfigManagerService();
        container.RegisterInstance(config);
        container.RegisterInstance<IConfigManagerService>(configManager);

        var service = new HttpServerService(container, config);
        await service.StartAsync();

        try
        {
            Assert.NotEmpty(config.Jwt.SigningKey);
            Assert.Equal(1, configManager.SaveCount);
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Fact]
    public async Task StartAsync_MintsADifferentKeyForEveryServer()
    {
        // The reason a constant default was refused: a shared secret lets anyone holding it mint
        // Administrator tokens against every server that kept it.
        Assert.NotEqual(await MintedKeyAsync(), await MintedKeyAsync());

        static async Task<string> MintedKeyAsync()
        {
            var container = new Container();
            var config = new MoongateHttpConfig
            {
                Address = "127.0.0.1", Port = 0, Jwt = new() { SigningKey = string.Empty }
            };
            container.RegisterInstance(config);
            container.RegisterInstance<IConfigManagerService>(new StubConfigManagerService());

            var service = new HttpServerService(container, config);
            await service.StartAsync();
            await service.StopAsync();

            return config.Jwt.SigningKey;
        }
    }

    [Fact]
    public async Task StartAsync_SigningKeyAlreadyConfigured_LeavesItAloneAndSavesNothing()
    {
        // Rewriting the config file on every boot would be a surprising side effect, and re-minting would
        // throw away the key the operator chose.
        var configManager = new StubConfigManagerService();

        await using var server = await TestHttpServer.StartAsync(
            container => container.RegisterInstance<IConfigManagerService>(configManager)
        );

        Assert.Equal(TestHttpServer.SigningKey, server.Container.Resolve<MoongateHttpConfig>().Jwt.SigningKey);
        Assert.Equal(0, configManager.SaveCount);
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
