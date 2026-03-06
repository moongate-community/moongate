using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Client;
using Moongate.Server.Http;
using Moongate.Server.Services.Sessions;
using Moongate.Tests.Server.Http.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Http;

public class MoongateHttpServiceActiveSessionsEndpointTests
{
    [Test]
    public async Task ActiveSessionsEndpoint_WhenConfigured_ShouldReturnOnlyInGameSessions()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var sessionService = new GameNetworkSessionService();

        using var inGameSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var inGameClient = new MoongateTCPClient(inGameSocket);
        var inGameSession = sessionService.GetOrCreate(inGameClient);
        inGameSession.AccountId = (Serial)7;
        inGameSession.AccountType = AccountType.Administrator;
        inGameSession.CharacterId = (Serial)15;
        inGameSession.Character = new()
        {
            Id = (Serial)15,
            Name = "AdminPlayer"
        };

        using var notInGameSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var notInGameClient = new MoongateTCPClient(notInGameSocket);
        var notInGameSession = sessionService.GetOrCreate(notInGameClient);
        notInGameSession.AccountId = (Serial)8;
        notInGameSession.AccountType = AccountType.Regular;
        notInGameSession.CharacterId = default;

        var accountService = new TestAccountService
        {
            GetAccountAsyncImpl = accountId => Task.FromResult(
                                      accountId.Value == 7
                                          ? new UOAccountEntity
                                          {
                                              Id = (Serial)7,
                                              Username = "admin",
                                              Email = "admin@moongate.local",
                                              AccountType = AccountType.Administrator,
                                              IsLocked = false,
                                              CreatedUtc = DateTime.UtcNow,
                                              LastLoginUtc = DateTime.UtcNow,
                                              CharacterIds = []
                                          }
                                          : null
                                  )
        };

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = false
            },
            accountService,
            gameNetworkSessionService: sessionService
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var response = await http.GetAsync($"http://127.0.0.1:{port}/api/sessions/active");
            var payload = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var sessions = root.EnumerateArray().ToList();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(sessions.Count, Is.EqualTo(1));
                    Assert.That(sessions[0].GetProperty("accountId").GetString(), Is.EqualTo("7"));
                    Assert.That(sessions[0].GetProperty("username").GetString(), Is.EqualTo("admin"));
                    Assert.That(sessions[0].GetProperty("accountType").GetString(), Is.EqualTo("Administrator"));
                    Assert.That(sessions[0].GetProperty("characterId").GetString(), Is.EqualTo("15"));
                    Assert.That(sessions[0].GetProperty("characterName").GetString(), Is.EqualTo("AdminPlayer"));
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
