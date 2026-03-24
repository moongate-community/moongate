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
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Http;

public sealed class MoongateHttpServiceHelpTicketsEndpointTests
{
    private sealed class HelpTicketsEndpointServiceStub : IHelpTicketService
    {
        public List<HelpTicketEntity> Tickets { get; } = [];

        public Task<HelpTicketEntity?> AssignToAccountAsync(
            Serial ticketId,
            Serial assignedToAccountId,
            Serial? assignedToCharacterId,
            CancellationToken cancellationToken = default
        )
        {
            var ticket = Tickets.FirstOrDefault(existing => existing.Id == ticketId);

            if (ticket is null)
            {
                return Task.FromResult<HelpTicketEntity?>(null);
            }

            ticket.Status = HelpTicketStatus.Assigned;
            ticket.AssignedToAccountId = assignedToAccountId;
            ticket.AssignedToCharacterId = assignedToCharacterId ?? Serial.Zero;
            ticket.AssignedAtUtc = DateTime.UtcNow;
            ticket.LastUpdatedAtUtc = DateTime.UtcNow;

            return Task.FromResult<HelpTicketEntity?>(ticket);
        }

        public Task<HelpTicketEntity?> CreateTicketAsync(
            long sessionId,
            HelpTicketCategory category,
            string message,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<HelpTicketEntity?>(null);

        public Task<IReadOnlyList<HelpTicketEntity>> GetAllTicketsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HelpTicketEntity>>([.. Tickets.OrderBy(ticket => ticket.CreatedAtUtc)]);

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

        public Task<HelpTicketEntity?> GetTicketByIdAsync(Serial ticketId, CancellationToken cancellationToken = default)
            => Task.FromResult<HelpTicketEntity?>(Tickets.FirstOrDefault(ticket => ticket.Id == ticketId));

        public Task<(IReadOnlyList<HelpTicketEntity> Items, int TotalCount)> GetTicketsForAdminAsync(
            int page,
            int pageSize,
            HelpTicketStatus? status,
            HelpTicketCategory? category,
            Serial? assignedToAccountId,
            CancellationToken cancellationToken = default
        )
        {
            var safePage = Math.Max(page, 1);
            var safePageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 200);
            IEnumerable<HelpTicketEntity> tickets = Tickets;

            if (status is not null)
            {
                tickets = tickets.Where(ticket => ticket.Status == status.Value);
            }

            if (category is not null)
            {
                tickets = tickets.Where(ticket => ticket.Category == category.Value);
            }

            if (assignedToAccountId is not null)
            {
                tickets = tickets.Where(ticket => ticket.AssignedToAccountId == assignedToAccountId.Value);
            }

            var filtered = tickets.OrderByDescending(ticket => ticket.CreatedAtUtc).ToList();
            var items = filtered.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList();

            return Task.FromResult<(IReadOnlyList<HelpTicketEntity>, int)>((items, filtered.Count));
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;

        public Task<HelpTicketEntity?> UpdateStatusAsync(
            Serial ticketId,
            HelpTicketStatus status,
            CancellationToken cancellationToken = default
        )
        {
            var ticket = Tickets.FirstOrDefault(existing => existing.Id == ticketId);

            if (ticket is null)
            {
                return Task.FromResult<HelpTicketEntity?>(null);
            }

            ticket.Status = status;

            if (status == HelpTicketStatus.Closed)
            {
                ticket.ClosedAtUtc = DateTime.UtcNow;
            }
            ticket.LastUpdatedAtUtc = DateTime.UtcNow;

            return Task.FromResult<HelpTicketEntity?>(ticket);
        }
    }

    [Test]
    public async Task HelpTicketAssignToMeEndpoint_WhenAuthenticatedAdmin_ShouldAssignTicket()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var accountService = CreateAccountService();
        var helpTickets = new HelpTicketsEndpointServiceStub();
        var ticket = CreateTicket((Serial)(Serial.ItemOffset + 20), (Serial)8, HelpTicketStatus.Open, "Question");
        helpTickets.Tickets.Add(ticket);

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
            var response = await http.PutAsync(
                               $"http://127.0.0.1:{port}/api/help-tickets/{ticket.Id.Value}/assign-to-me",
                               JsonContent.Create(new { })
                           );
            var updated = await response.Content.ReadFromJsonAsync<MoongateHttpHelpTicket>();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(updated, Is.Not.Null);
                    Assert.That(updated!.Status, Is.EqualTo("Assigned"));
                    Assert.That(updated.AssignedToAccountId, Is.EqualTo("7"));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task HelpTicketByIdEndpoint_WhenAuthenticatedAdmin_ShouldReturnTicket()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var accountService = CreateAccountService();
        var helpTickets = new HelpTicketsEndpointServiceStub();
        var expected = CreateTicket((Serial)(Serial.ItemOffset + 9), (Serial)7, HelpTicketStatus.Open, "Question");
        helpTickets.Tickets.Add(expected);

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
            var response = await http.GetAsync($"http://127.0.0.1:{port}/api/help-tickets/{expected.Id.Value}");
            var ticket = await response.Content.ReadFromJsonAsync<MoongateHttpHelpTicket>();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(ticket, Is.Not.Null);
                    Assert.That(ticket!.TicketId, Is.EqualTo(expected.Id.Value.ToString()));
                    Assert.That(ticket.Category, Is.EqualTo("Question"));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task HelpTicketsEndpoint_WhenAuthenticatedAdmin_ShouldReturnPagedResults()
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
            var response = await http.GetAsync($"http://127.0.0.1:{port}/api/help-tickets?page=1&pageSize=2");
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var items = payload.RootElement.GetProperty("items").EnumerateArray().ToList();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(payload.RootElement.GetProperty("page").GetInt32(), Is.EqualTo(1));
                    Assert.That(payload.RootElement.GetProperty("pageSize").GetInt32(), Is.EqualTo(2));
                    Assert.That(payload.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
                    Assert.That(items.Count, Is.EqualTo(2));
                    Assert.That(
                        items[0].GetProperty("ticketId").GetString(),
                        Is.EqualTo((Serial.ItemOffset + 3).ToString())
                    );
                    Assert.That(items[1].GetProperty("status").GetString(), Is.EqualTo("Assigned"));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task HelpTicketsEndpoint_WhenFiltersAreApplied_ShouldReturnMatchingPagedResults()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var accountService = CreateAccountService();
        var helpTickets = new HelpTicketsEndpointServiceStub();

        var mine = CreateTicket((Serial)(Serial.ItemOffset + 1), (Serial)7, HelpTicketStatus.Open, "Question");
        mine.AssignedToAccountId = (Serial)7;
        mine.AssignedAtUtc = mine.CreatedAtUtc;

        var otherCategory = CreateTicket((Serial)(Serial.ItemOffset + 2), (Serial)7, HelpTicketStatus.Open, "Bug");
        var otherStatus = CreateTicket((Serial)(Serial.ItemOffset + 3), (Serial)7, HelpTicketStatus.Closed, "Question");

        helpTickets.Tickets.AddRange([mine, otherCategory, otherStatus]);

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
            var response = await http.GetAsync(
                               $"http://127.0.0.1:{port}/api/help-tickets?page=1&pageSize=50&status=Open&category=Question&assignedToMe=true"
                           );
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var items = payload.RootElement.GetProperty("items").EnumerateArray().ToList();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(payload.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
                    Assert.That(items.Count, Is.EqualTo(1));
                    Assert.That(
                        items[0].GetProperty("ticketId").GetString(),
                        Is.EqualTo((Serial.ItemOffset + 1).ToString())
                    );
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

    [Test]
    public async Task HelpTicketStatusEndpoint_WhenAuthenticatedAdmin_ShouldUpdateStatus()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var accountService = CreateAccountService();
        var helpTickets = new HelpTicketsEndpointServiceStub();
        var ticket = CreateTicket((Serial)(Serial.ItemOffset + 21), (Serial)8, HelpTicketStatus.Assigned, "Question");
        helpTickets.Tickets.Add(ticket);

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
            var response = await http.PutAsJsonAsync(
                               $"http://127.0.0.1:{port}/api/help-tickets/{ticket.Id.Value}/status",
                               new { status = "Closed" }
                           );
            var updated = await response.Content.ReadFromJsonAsync<MoongateHttpHelpTicket>();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(updated, Is.Not.Null);
                    Assert.That(updated!.Status, Is.EqualTo("Closed"));
                    Assert.That(updated.ClosedAtUtc, Is.Not.Null);
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

        return new()
        {
            LoginAsyncImpl = (username, password) =>
                                 Task.FromResult<UOAccountEntity?>(
                                     username == "admin" && password == "secret" ? admin :
                                     username == "player_one" && password == "secret" ? player : null
                                 ),
            GetAccountAsyncImpl = accountId =>
                                      Task.FromResult<UOAccountEntity?>(
                                          accountId == admin.Id ? admin :
                                          accountId == player.Id ? player : null
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
            Category = Enum.Parse<HelpTicketCategory>(categoryName, true),
            Message = $"ticket-{id.Value}",
            MapId = 0,
            Location = new(1443, 1692, 0),
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
