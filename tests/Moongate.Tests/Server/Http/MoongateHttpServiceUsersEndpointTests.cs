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

public class MoongateHttpServiceUsersEndpointTests
{
    [Test]
    public async Task UsersEndpoint_WhenConfigured_ShouldReturnUsers()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();

        var accountService = new TestAccountService
        {
            GetAccountsAsyncImpl = _ => Task.FromResult<IReadOnlyList<UOAccountEntity>>(
                [
                    new UOAccountEntity
                    {
                        Id = (Moongate.UO.Data.Ids.Serial)1,
                        Username = "admin",
                        Email = "admin@moongate.local",
                        AccountType = AccountType.Administrator,
                        IsLocked = false,
                        CreatedUtc = DateTime.UtcNow,
                        LastLoginUtc = DateTime.UtcNow,
                        CharacterIds = [(Moongate.UO.Data.Ids.Serial)2]
                    }
                ]
            )
        };

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = false
            },
            accountService
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var response = await http.GetAsync($"http://127.0.0.1:{port}/api/users/");
            var payload = await response.Content.ReadFromJsonAsync<List<MoongateHttpUser>>();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(payload, Is.Not.Null);
                    Assert.That(payload!.Count, Is.EqualTo(1));
                    Assert.That(payload[0].Username, Is.EqualTo("admin"));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task UsersCrudEndpoints_WhenConfigured_ShouldSupportCreateUpdateDelete()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var users = new Dictionary<string, UOAccountEntity>(StringComparer.Ordinal)
        {
            ["10"] = new UOAccountEntity
            {
                Id = (Moongate.UO.Data.Ids.Serial)10,
                Username = "seed",
                Email = "seed@moongate.local",
                AccountType = AccountType.Regular,
                IsLocked = false,
                CreatedUtc = DateTime.UtcNow,
                LastLoginUtc = DateTime.UtcNow,
                CharacterIds = []
            }
        };

        var accountService = new TestAccountService
        {
            GetAccountsAsyncImpl = _ => Task.FromResult<IReadOnlyList<UOAccountEntity>>([.. users.Values]),
            GetAccountAsyncImpl = accountId =>
            {
                users.TryGetValue(accountId.Value.ToString(), out var user);

                return Task.FromResult(user);
            },
            CreateAccountAsyncImpl = (username, _, email, role) =>
            {
                if (users.Values.Any(static u => u.Username.Equals("admin", StringComparison.Ordinal)))
                {
                    return Task.FromResult<UOAccountEntity?>(null);
                }

                var created = new UOAccountEntity
                {
                    Id = (Moongate.UO.Data.Ids.Serial)11,
                    Username = username,
                    Email = email,
                    AccountType = role,
                    IsLocked = false,
                    CreatedUtc = DateTime.UtcNow,
                    LastLoginUtc = DateTime.UtcNow,
                    CharacterIds = []
                };
                users[created.Id.Value.ToString()] = created;

                return Task.FromResult<UOAccountEntity?>(created);
            },
            UpdateAccountAsyncImpl = (accountId, username, _, email, role, isLocked, _) =>
            {
                if (!users.TryGetValue(accountId.Value.ToString(), out var existing))
                {
                    return Task.FromResult<UOAccountEntity?>(null);
                }

                var updated = new UOAccountEntity
                {
                    Id = existing.Id,
                    Username = username ?? existing.Username,
                    Email = email ?? existing.Email,
                    AccountType = role ?? existing.AccountType,
                    IsLocked = isLocked ?? existing.IsLocked,
                    CreatedUtc = existing.CreatedUtc,
                    LastLoginUtc = existing.LastLoginUtc,
                    CharacterIds = [.. existing.CharacterIds]
                };
                users[accountId.Value.ToString()] = updated;

                return Task.FromResult<UOAccountEntity?>(updated);
            },
            DeleteAccountAsyncImpl = accountId => Task.FromResult(users.Remove(accountId.Value.ToString()))
        };

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = false
            },
            accountService
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();

            var createResponse = await http.PostAsJsonAsync(
                                     $"http://127.0.0.1:{port}/api/users/",
                                     new MoongateHttpCreateUserRequest
                                     {
                                         Username = "admin",
                                         Password = "secret",
                                         Email = "admin@moongate.local",
                                         Role = "Administrator"
                                     }
                                 );

            var updatedResponse = await http.PutAsJsonAsync(
                                      $"http://127.0.0.1:{port}/api/users/11",
                                      new MoongateHttpUpdateUserRequest
                                      {
                                          Email = "root@moongate.local",
                                          IsLocked = true
                                      }
                                  );

            var deleteResponse = await http.DeleteAsync($"http://127.0.0.1:{port}/api/users/11");
            var getDeletedResponse = await http.GetAsync($"http://127.0.0.1:{port}/api/users/11");

            var createdPayload = await createResponse.Content.ReadFromJsonAsync<MoongateHttpUser>();
            var updatedPayload = await updatedResponse.Content.ReadFromJsonAsync<MoongateHttpUser>();

            Assert.Multiple(
                () =>
                {
                    Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                    Assert.That(createdPayload, Is.Not.Null);
                    Assert.That(createdPayload!.Username, Is.EqualTo("admin"));
                    Assert.That(updatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(updatedPayload, Is.Not.Null);
                    Assert.That(updatedPayload!.Email, Is.EqualTo("root@moongate.local"));
                    Assert.That(updatedPayload.IsLocked, Is.True);
                    Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
                    Assert.That(getDeletedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task UserByIdEndpoint_WhenConfigured_ShouldReturnUserOrNotFound()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();

        var accountService = new TestAccountService
        {
            GetAccountsAsyncImpl = _ => Task.FromResult<IReadOnlyList<UOAccountEntity>>([]),
            GetAccountAsyncImpl = accountId => Task.FromResult<UOAccountEntity?>(
                accountId.Value == 42
                    ? new UOAccountEntity
                    {
                        Id = (Moongate.UO.Data.Ids.Serial)42,
                        Username = "test",
                        Email = "test@moongate.local",
                        AccountType = AccountType.Regular,
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
            accountService
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var okResponse = await http.GetAsync($"http://127.0.0.1:{port}/api/users/42");
            var notFoundResponse = await http.GetAsync($"http://127.0.0.1:{port}/api/users/99");

            Assert.Multiple(
                () =>
                {
                    Assert.That(okResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(notFoundResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
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
