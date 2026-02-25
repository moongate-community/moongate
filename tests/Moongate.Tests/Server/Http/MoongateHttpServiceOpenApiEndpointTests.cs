using System.Net;
using System.Net.Sockets;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Http;
using Moongate.Server.Http.Data;
using Moongate.Server.Http.Data.Results;
using Moongate.Tests.Server.Http.Support;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server.Http;

public class MoongateHttpServiceOpenApiEndpointTests
{
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
    public async Task OpenApiEndpoint_WhenJwtEnabled_ShouldContainAuthLoginRoute()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();

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
                },
                AuthFacade = new TestHttpAuthFacade(
                    (_, _, _) => Task.FromResult(
                        MoongateHttpOperationResult<MoongateHttpAuthenticatedUser>.Unauthorized()
                    )
                )
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
    public async Task OpenApiEndpoint_WhenUsersCrudFacadeConfigured_ShouldContainUsersCrudRoutes()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = true,
                UsersFacade = new TestHttpUsersFacade(
                    _ => Task.FromResult(MoongateHttpOperationResult<IReadOnlyList<MoongateHttpUser>>.Ok([])),
                    (_, _) => Task.FromResult(MoongateHttpOperationResult<MoongateHttpUser>.NotFound()),
                    (_, _) => Task.FromResult(MoongateHttpOperationResult<MoongateHttpUser>.Conflict()),
                    (_, _, _) => Task.FromResult(MoongateHttpOperationResult<MoongateHttpUser>.NotFound()),
                    (_, _) => Task.FromResult(MoongateHttpOperationResult<object?>.NotFound())
                )
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
