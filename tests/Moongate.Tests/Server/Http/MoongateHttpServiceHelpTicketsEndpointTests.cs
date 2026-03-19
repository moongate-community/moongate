using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Http;
using Moongate.Server.Http.Data;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Tests.Server.Http.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Http;

public sealed class MoongateHttpServiceHelpTicketsEndpointTests
{
    private sealed class HelpTicketsEndpointServiceStub : IHelpTicketService
    {
        public List<HelpTicketEntity> Tickets { get; } = [];

        public Task<HelpTicketEntity?> CreateTicketAsync(
            long sessionId,
            HelpTicketCategory category,
            string message,
            CancellationToken cancellationToken = default
            )
            => Task.FromResult<HelpTicketEntity?>(null);

        public Task<IReadOnlyList<HelpTicketEntity>> GetAllTicketsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HelpTicketEntity>>(
                [.. Tickets.OrderBy(ticket => ticket.CreatedAtUtc)]
            );

        public Task<IReadOnlyList<HelpTicketEntity>> GetOpenTicketsForAccountAsync(
            Serial senderAccountId,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<IReadOnlyList<HelpTicketEntity>>(
                [
                    .. Tickets.Where(
                                   ticket => ticket.SenderAccountId == senderAccountId &&
                                             ticket.Status is HelpTicketStatus.Open or HelpTicketStatus.Assigned
                               )
                              .OrderBy(ticket => ticket.CreatedAtUtc)
                ]
            );

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    [Test]
    public async Task HelpTicketsEndpoint_WhenAuthenticatedAdmin_ShouldReturnAllTickets()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var accountService = CreateAccountService();
        var helpTickets = new HelpTicketsEndpointServiceStub();
        helpTickets.Tickets.AddRange(
            [
                CreateTicket((Serial)(Serial.ItemOffset + 1), (Serial)7, HelpTicketStatus.Open, "Question"),
                CreateTicket((Serial)(Serial.ItemOffset + 2), (Serial)8, HelpTicketStatus.Assigned, "Bug"),
                CreateTicket((Serial)(Serial.ItemOffset + 3), (Serial)8, HelpTicketStatus.Closed, "Other")
            ]
        );

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
            accountService,
            helpTicketService: helpTickets
        );

        await service.StartAsync();

        try
        {
            using var http = await LoginAsync(port, "admin", "secret");
            var response = await http.GetAsync($"http://127.0.0.1:{port}/api/help-tickets");
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var tickets = payload.RootElement.EnumerateArray().ToList();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(tickets.Count, Is.EqualTo(3));
                    Assert.That(tickets[0].GetProperty("ticketId").GetString(), Is.EqualTo((Serial.ItemOffset + 1).ToString()));
                    Assert.That(tickets[1].GetProperty("status").GetString(), Is.EqualTo("Assigned"));
                    Assert.That(tickets[2].GetProperty("category").GetString(), Is.EqualTo("Other"));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task HelpTicketsMeEndpoint_WhenAuthenticatedPlayer_ShouldReturnOwnOpenAndAssignedTickets()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var accountService = CreateAccountService();
        var helpTickets = new HelpTicketsEndpointServiceStub();
        helpTickets.Tickets.AddRange(
            [
                CreateTicket((Serial)(Serial.ItemOffset + 1), (Serial)8, HelpTicketStatus.Open, "Question"),
                CreateTicket((Serial)(Serial.ItemOffset + 2), (Serial)8, HelpTicketStatus.Assigned, "Bug"),
                CreateTicket((Serial)(Serial.ItemOffset + 3), (Serial)8, HelpTicketStatus.Closed, "Other"),
                CreateTicket((Serial)(Serial.ItemOffset + 4), (Serial)7, HelpTicketStatus.Open, "Suggestion")
            ]
        );

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
            accountService,
            helpTicketService: helpTickets
        );

        await service.StartAsync();

        try
        {
            using var http = await LoginAsync(port, "player_one", "secret");
            var response = await http.GetAsync($"http://127.0.0.1:{port}/api/help-tickets/me");
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var tickets = payload.RootElement.EnumerateArray().ToList();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(tickets.Count, Is.EqualTo(2));
                    Assert.That(tickets.All(ticket => ticket.GetProperty("senderAccountId").GetString() == "8"), Is.True);
                    Assert.That(tickets.Any(ticket => ticket.GetProperty("status").GetString() == "Closed"), Is.False);
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    private static TestAccountService CreateAccountService()
    {
        var admin = new UOAccountEntity
        {
            Id = (Serial)7,
            Username = "admin",
            Email = "admin@moongate.test",
            AccountType = AccountType.Administrator
        };
        var player = new UOAccountEntity
        {
            Id = (Serial)8,
            Username = "player_one",
            Email = "player@moongate.test",
            AccountType = AccountType.Regular
        };

        return new TestAccountService
        {
            LoginAsyncImpl = (username, password) =>
                                 Task.FromResult<UOAccountEntity?>(
                                     username == "admin" && password == "secret"
                                         ? admin
                                         : username == "player_one" && password == "secret"
                                             ? player
                                             : null
                                 ),
            GetAccountAsyncImpl = accountId =>
                                      Task.FromResult<UOAccountEntity?>(
                                          accountId == admin.Id
                                              ? admin
                                              : accountId == player.Id
                                                  ? player
                                                  : null
                                      )
        };
    }

    private static HelpTicketEntity CreateTicket(
        Serial id,
        Serial senderAccountId,
        HelpTicketStatus status,
        string categoryName
    )
        => new()
        {
            Id = id,
            SenderCharacterId = (Serial)0x00000042u,
            SenderAccountId = senderAccountId,
            Category = Enum.Parse<HelpTicketCategory>(categoryName, ignoreCase: true),
            Message = $"ticket-{id.Value}",
            MapId = 0,
            Location = new Point3D(1443, 1692, 0),
            Status = status,
            CreatedAtUtc = new DateTime(2026, 3, 19, 10, 0, 0, DateTimeKind.Utc).AddMinutes(id.Value),
            LastUpdatedAtUtc = new DateTime(2026, 3, 19, 10, 0, 0, DateTimeKind.Utc).AddMinutes(id.Value)
        };

    private static int GetRandomPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        return endpoint.Port;
    }

    private static async Task<HttpClient> LoginAsync(int port, string username, string password)
    {
        var http = new HttpClient();
        var loginResponse = await http.PostAsJsonAsync(
                                $"http://127.0.0.1:{port}/auth/login",
                                new MoongateHttpLoginRequest
                                {
                                    Username = username,
                                    Password = password
                                }
                            );
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<MoongateHttpLoginResponse>();
        http.DefaultRequestHeaders.Authorization = new("Bearer", loginPayload!.AccessToken);
        return http;
    }
}
