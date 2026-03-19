using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Http;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server.Http;

public class MoongateHttpServiceBrandingEndpointTests
{
    private sealed class BrandingPayload
    {
        public string? ShardName { get; set; }

        public string? AdminLoginLogoUrl { get; set; }

        public string? PlayerLoginLogoUrl { get; set; }
    }

    [Test]
    public async Task BrandingEndpoint_ShouldReturnConfiguredShardNameAndLoginLogos()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetValues<DirectoryType>().Cast<Enum>().ToArray());
        var port = GetRandomPort();

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = false,
                ShardName = "Moongate Test",
                AdminLoginLogoPath = "images/moongate_logo_admin.png",
                PlayerLoginLogoPath = "images/moongate_logo_players.png"
            }
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var response = await http.GetAsync($"http://127.0.0.1:{port}/api/branding");
            var payload = await response.Content.ReadFromJsonAsync<BrandingPayload>();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(payload, Is.Not.Null);
                    Assert.That(payload!.ShardName, Is.EqualTo("Moongate Test"));
                    Assert.That(payload.AdminLoginLogoUrl, Is.EqualTo("/images/moongate_logo_admin.png"));
                    Assert.That(payload.PlayerLoginLogoUrl, Is.EqualTo("/images/moongate_logo_players.png"));
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
