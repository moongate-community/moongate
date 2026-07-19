using System.Net;
using DryIoc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongate.Http.Plugin.Data.Config;
using Moongate.Http.Plugin.Interfaces.Endpoints;
using Moongate.Http.Plugin.Services.Hosting;
using Moongate.Tests.Support;
using SquidStd.Core.Interfaces.Config;

namespace Moongate.Tests.Http.Services.Hosting;

public class HttpServerServiceTests
{
    private sealed class ProbeEndpoints : IApiEndpointRegistration
    {
        public void Register(IEndpointRouteBuilder routes)
            => routes.MapGet("/probe", () => Results.Ok("mapped"));
    }

    /// <summary>
    /// Documented with a method group rather than a lambda on purpose: that is the shape the real endpoint
    /// groups use, and the only shape whose <c>///</c> Swashbuckle reads.
    /// </summary>
    private sealed class DocumentedProbeEndpoints : IApiEndpointRegistration
    {
        internal const string Summary = "Documented probe summary.";

        internal const string Remarks = "Documented probe remarks.";

        public void Register(IEndpointRouteBuilder routes)
            => routes.MapGet("/documented-probe", Handle);

        /// <summary>Documented probe summary.</summary>
        /// <remarks>Documented probe remarks.</remarks>
        private static IResult Handle()
            => Results.Ok("documented");
    }

    /// <summary>Stands in for any game singleton holding an OS resource, as several really do.</summary>
    private sealed class DisposableGameSingleton : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
            => Disposed = true;
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
    public async Task StartAsync_MapsRegisteredEndpointGroups()
    {
        await using var server = await TestHttpServer.StartAsync(
                                     container => container.RegisterApiEndpointInstance(new ProbeEndpoints())
                                 );

        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/probe")).StatusCode);
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
    public async Task StartAsync_PutsEndpointXmlCommentsIntoTheDocument()
    {
        // The whole point of the /// on the endpoint handlers: without the XML wired in, the document
        // still serves and Scalar still renders — just with every description blank. Nothing else here
        // would notice, which is why this asserts on the prose rather than on a 200.
        await using var server = await TestHttpServer.StartAsync(
                                     container => container.RegisterApiEndpointInstance(new DocumentedProbeEndpoints())
                                 );

        var document = await server.Client.GetStringAsync(HttpServerService.SwaggerDocumentRoute);

        Assert.Contains(DocumentedProbeEndpoints.Summary, document, StringComparison.Ordinal);
        Assert.Contains(DocumentedProbeEndpoints.Remarks, document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_ScalarPointsAtTheDocumentThatIsActuallyServed()
    {
        // Asserting that /scalar/v1 answers 200 proves nothing: the page renders whatever happens, so
        // pointing it at a document nobody serves still yields a 200 — showing an empty reference, which
        // is exactly the bug this catches. What matters is the document it will go and fetch.
        //
        // Scalar's integration script resolves each source against the origin plus the app's base path
        // (`new URL(source.url, origin + basePath + '/')`), so the route is emitted relative and is
        // matched here the same way.
        await using var server = await TestHttpServer.StartAsync();

        var page = await server.Client.GetStringAsync("/scalar/v1");

        Assert.Contains(HttpServerService.SwaggerDocumentRoute.TrimStart('/'), page, StringComparison.Ordinal);

        // Scalar's own default, which nothing here serves: the reference silently renders empty against it.
        Assert.DoesNotContain("openapi/v1.json", page, StringComparison.Ordinal);

        Assert.Equal(
            HttpStatusCode.OK,
            (await server.Client.GetAsync(HttpServerService.SwaggerDocumentRoute)).StatusCode
        );
    }

    [Fact]
    public async Task StartAsync_ServesHealth()
    {
        await using var server = await TestHttpServer.StartAsync();

        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task StartAsync_ServesTheOpenApiDocument()
    {
        await using var server = await TestHttpServer.StartAsync();

        Assert.Equal(
            HttpStatusCode.OK,
            (await server.Client.GetAsync(HttpServerService.SwaggerDocumentRoute)).StatusCode
        );
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
    public async Task StopAsync_LeavesTheGameContainersSingletonsAlone()
    {
        // The web app runs on the game's container, so disposing the WebApplication disposes that
        // container's singletons — the game's, not the API's. They would go down while the game is still
        // running, and its own StopAsync would then fail on objects already disposed.
        var server = await TestHttpServer.StartAsync(
                         container => container.Register<DisposableGameSingleton>(Reuse.Singleton)
                     );
        var singleton = server.Container.Resolve<DisposableGameSingleton>();

        await server.DisposeAsync();

        Assert.False(singleton.Disposed);
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
