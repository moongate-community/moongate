using System.Net;
using System.Net.Sockets;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Http;
using Moongate.Server.Services.Sessions;
using Moongate.Tests.Server.Http.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Services.Templates;

namespace Moongate.Tests.Server.Http;

public class MoongateHttpServiceOpenApiEndpointTests
{
    [Test]
    public async Task OpenApiEndpoint_WhenActiveSessionsConfigured_ShouldContainActiveSessionsRoute()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var sessionsService = new GameNetworkSessionService();

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = true
            },
            gameNetworkSessionService: sessionsService
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var response = await http.GetAsync($"http://127.0.0.1:{port}/openapi/v1.json");
            var payload = await response.Content.ReadAsStringAsync();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(payload, Does.Contain("\"/api/sessions/active\""));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task OpenApiEndpoint_WhenEnabled_ShouldContainMappedRoutes()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = true
            }
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var response = await http.GetAsync($"http://127.0.0.1:{port}/openapi/v1.json");
            var payload = await response.Content.ReadAsStringAsync();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(payload, Does.Contain("\"/health\""));
                    Assert.That(payload, Does.Contain("\"/metrics\""));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task OpenApiEndpoint_WhenItemTemplatesConfigured_ShouldContainItemTemplateRoutes()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var itemTemplateService = new ItemTemplateService();
        itemTemplateService.Upsert(
            new()
            {
                Id = "test_item",
                Name = "Test",
                Category = "test",
                ItemId = "0x1F9E"
            }
        );

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = true
            },
            itemTemplateService: itemTemplateService,
            artService: new TestArtService()
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var response = await http.GetAsync($"http://127.0.0.1:{port}/openapi/v1.json");
            var payload = await response.Content.ReadAsStringAsync();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(payload, Does.Contain("\"/api/item-templates\""));
                    Assert.That(payload, Does.Contain("\"/api/item-templates/{id}\""));
                    Assert.That(payload, Does.Contain("\"/api/item-templates/by-item-id/{itemId}/image\""));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task OpenApiEndpoint_WhenJwtEnabled_ShouldContainAuthLoginRoute()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var accountService = new TestAccountService
        {
            LoginAsyncImpl = (_, _) => Task.FromResult<UOAccountEntity?>(null)
        };

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = true,
                Jwt = new()
                {
                    IsEnabled = true,
                    SigningKey = "moongate-http-tests-signing-key-at-least-32-bytes",
                    Issuer = "moongate-tests",
                    Audience = "moongate-tests-client",
                    ExpirationMinutes = 5
                }
            },
            accountService
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var response = await http.GetAsync($"http://127.0.0.1:{port}/openapi/v1.json");
            var payload = await response.Content.ReadAsStringAsync();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(payload, Does.Contain("\"/auth/login\""));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task OpenApiEndpoint_WhenUsersCrudCallbacksConfigured_ShouldContainUsersCrudRoutes()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();

        var accountService = new TestAccountService();

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = true
            },
            accountService
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var response = await http.GetAsync($"http://127.0.0.1:{port}/openapi/v1.json");
            var payload = await response.Content.ReadAsStringAsync();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(payload, Does.Contain("\"/api/users\""));
                    Assert.That(payload, Does.Contain("\"post\""));
                    Assert.That(payload, Does.Contain("\"/api/users/{accountId}\""));
                    Assert.That(payload, Does.Contain("\"put\""));
                    Assert.That(payload, Does.Contain("\"delete\""));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    private static int GetRandomPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;

        return endpoint.Port;
    }
}
