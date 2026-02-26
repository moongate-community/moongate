using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Http;
using Moongate.Server.Http.Data;
using Moongate.Tests.Server.Http.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Http;

public class MoongateHttpServiceJwtLoginEndpointTests
{
    [Test]
    public async Task LoginEndpoint_WhenJwtEnabled_ShouldIssueTokenForValidCredentials()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();

        var accountService = new TestAccountService
        {
            LoginAsyncImpl = (username, password) => Task.FromResult<UOAccountEntity?>(
                username == "admin" && password == "admin"
                    ? new UOAccountEntity
                    {
                        Id = (Moongate.UO.Data.Ids.Serial)1,
                        Username = "admin",
                        AccountType = AccountType.Administrator
                    }
                    : null
            )
        };

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = false,
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
            var response = await http.PostAsJsonAsync(
                               $"http://127.0.0.1:{port}/auth/login",
                               new MoongateHttpLoginRequest
                               {
                                   Username = "admin",
                                   Password = "admin"
                               }
                           );

            var payload = await response.Content.ReadFromJsonAsync<MoongateHttpLoginResponse>();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(payload, Is.Not.Null);
                    Assert.That(payload!.AccessToken, Is.Not.Empty);
                    Assert.That(payload.Username, Is.EqualTo("admin"));
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
