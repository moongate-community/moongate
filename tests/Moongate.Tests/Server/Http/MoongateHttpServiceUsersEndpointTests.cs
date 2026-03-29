using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Http;
using Moongate.Server.Http.Data;
using Moongate.Tests.Server.Http.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Http;

public class MoongateHttpServiceUsersEndpointTests
{
    [Test]
    public async Task UsersEndpoints_WhenJwtIsDisabled_ShouldNotBeMapped()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var accountService = CreateAccountService();

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
            var listResponse = await http.GetAsync($"http://127.0.0.1:{port}/api/users/");
            var createResponse = await http.PostAsJsonAsync(
                                     $"http://127.0.0.1:{port}/api/users/",
                                     new MoongateHttpCreateUserRequest
                                     {
                                         Username = "new-user",
                                         Password = "secret",
                                         Role = "Regular"
                                     }
                                 );

            Assert.Multiple(
                () =>
                {
                    Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                    Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task UsersEndpoints_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var accountService = CreateAccountService();

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
            var getResponse = await http.GetAsync($"http://127.0.0.1:{port}/api/users/");
            var getByIdResponse = await http.GetAsync($"http://127.0.0.1:{port}/api/users/10");
            var createResponse = await http.PostAsJsonAsync(
                                     $"http://127.0.0.1:{port}/api/users/",
                                     new MoongateHttpCreateUserRequest
                                     {
                                         Username = "no-auth",
                                         Password = "secret",
                                         Role = "Regular"
                                     }
                                 );

            var updateResponse = await http.PutAsJsonAsync(
                                      $"http://127.0.0.1:{port}/api/users/11",
                                      new MoongateHttpUpdateUserRequest
                                      {
                                          Email = "blocked@moongate.local"
                                      }
                                  );
            var deleteResponse = await http.DeleteAsync($"http://127.0.0.1:{port}/api/users/11");

            Assert.Multiple(
                () =>
                {
                    Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                    Assert.That(getByIdResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                    Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                    Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                    Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task UsersEndpoints_WhenAuthenticatedNonAdmin_ShouldReturnForbidden()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var accountService = CreateAccountService();

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
            using var http = await LoginAsync(port, "player_one", "secret");
            var getResponse = await http.GetAsync($"http://127.0.0.1:{port}/api/users/");
            var getByIdResponse = await http.GetAsync($"http://127.0.0.1:{port}/api/users/10");
            var createResponse = await http.PostAsJsonAsync(
                                     $"http://127.0.0.1:{port}/api/users/",
                                     new MoongateHttpCreateUserRequest
                                     {
                                         Username = "player-created",
                                         Password = "secret",
                                         Role = "Regular"
                                     }
                                 );
            var updateResponse = await http.PutAsJsonAsync(
                                     $"http://127.0.0.1:{port}/api/users/10",
                                     new MoongateHttpUpdateUserRequest
                                     {
                                         Email = "player-change@moongate.local"
                                     }
                                 );
            var deleteResponse = await http.DeleteAsync($"http://127.0.0.1:{port}/api/users/10");

            Assert.Multiple(
                () =>
                {
                    Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                    Assert.That(getByIdResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                    Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                    Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                    Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task UsersCrudEndpoints_WhenAuthenticatedAdmin_ShouldSupportCreateUpdateDelete()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();
        var accountService = CreateAccountService();

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
            using var http = await LoginAsync(port, "admin", "secret");
            var createResponse = await http.PostAsJsonAsync(
                                     $"http://127.0.0.1:{port}/api/users/",
                                     new MoongateHttpCreateUserRequest
                                     {
                                         Username = "created-admin",
                                         Password = "secret",
                                         Email = "created-admin@moongate.local",
                                         Role = "Administrator"
                                     }
                                 );

            var createdPayload = await createResponse.Content.ReadFromJsonAsync<MoongateHttpUser>();
            var createdId = createdPayload?.AccountId ?? "0";

            var updateResponse = await http.PutAsJsonAsync(
                                     $"http://127.0.0.1:{port}/api/users/{createdId}",
                                     new MoongateHttpUpdateUserRequest
                                     {
                                         Email = "root@moongate.local",
                                         Role = "GameMaster",
                                         IsLocked = true
                                     }
                                 );
            var updatedPayload = await updateResponse.Content.ReadFromJsonAsync<MoongateHttpUser>();

            var getByIdResponse = await http.GetAsync($"http://127.0.0.1:{port}/api/users/{createdId}");
            var byIdPayload = await getByIdResponse.Content.ReadFromJsonAsync<MoongateHttpUser>();

            var deleteResponse = await http.DeleteAsync($"http://127.0.0.1:{port}/api/users/{createdId}");
            var getDeletedResponse = await http.GetAsync($"http://127.0.0.1:{port}/api/users/{createdId}");

            Assert.Multiple(
                () =>
                {
                    Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
                    Assert.That(createdPayload, Is.Not.Null);
                    Assert.That(createdPayload!.Username, Is.EqualTo("created-admin"));
                    Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(updatedPayload, Is.Not.Null);
                    Assert.That(updatedPayload!.Email, Is.EqualTo("root@moongate.local"));
                    Assert.That(updatedPayload.Role, Is.EqualTo("GameMaster"));
                    Assert.That(updatedPayload.IsLocked, Is.True);
                    Assert.That(getByIdResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(byIdPayload, Is.Not.Null);
                    Assert.That(byIdPayload!.Role, Is.EqualTo("GameMaster"));
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

    private static TestAccountService CreateAccountService()
    {
        var users = new Dictionary<string, UOAccountEntity>(StringComparer.Ordinal)
        {
            ["10"] = new()
            {
                Id = (Serial)10,
                Username = "seed",
                Email = "seed@moongate.local",
                AccountType = AccountType.Regular,
                IsLocked = false,
                CreatedUtc = DateTime.UtcNow,
                LastLoginUtc = DateTime.UtcNow,
                CharacterIds = []
            }
        };

        var admin = new UOAccountEntity
        {
            Id = (Serial)7,
            Username = "admin",
            Email = "admin@moongate.test",
            AccountType = AccountType.Administrator,
            IsLocked = false,
            CreatedUtc = DateTime.UtcNow,
            LastLoginUtc = DateTime.UtcNow,
            CharacterIds = []
        };
        var player = new UOAccountEntity
        {
            Id = (Serial)8,
            Username = "player_one",
            Email = "player@moongate.test",
            AccountType = AccountType.Regular,
            IsLocked = false,
            CreatedUtc = DateTime.UtcNow,
            LastLoginUtc = DateTime.UtcNow,
            CharacterIds = []
        };

        return new()
        {
            LoginAsyncImpl = (username, password) =>
                                 Task.FromResult<UOAccountEntity?>(
                                     username == "admin" && password == "secret" ? admin :
                                     username == "player_one" && password == "secret" ? player : null
                                 ),
            GetAccountsAsyncImpl = _ => Task.FromResult<IReadOnlyList<UOAccountEntity>>([.. users.Values]),
            GetAccountAsyncImpl = accountId =>
                                  {
                                      if (accountId == admin.Id)
                                      {
                                          return Task.FromResult<UOAccountEntity?>(admin);
                                      }

                                      if (accountId == player.Id)
                                      {
                                          return Task.FromResult<UOAccountEntity?>(player);
                                      }

                                      users.TryGetValue(accountId.Value.ToString(), out var user);

                                      return Task.FromResult(user);
                                  },
            CreateAccountAsyncImpl = (username, _, email, role) =>
                                     {
                                         if (users.Values.Any(
                                                 user => user.Username.Equals(username, StringComparison.Ordinal)
                                             ))
                                         {
                                             return Task.FromResult<UOAccountEntity?>(null);
                                         }

                                         var nextId = users.Keys.Select(uint.Parse).DefaultIfEmpty(10u).Max() + 1u;
                                         var created = new UOAccountEntity
                                         {
                                             Id = (Serial)nextId,
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
            UpdateAccountAsyncImpl = (accountId, username, _, email, role, isLocked, allowPrivilegeChanges, _, _) =>
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
                                             AccountType = role.HasValue && allowPrivilegeChanges
                                                               ? role.Value
                                                               : existing.AccountType,
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
    }

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
        var payload = await loginResponse.Content.ReadFromJsonAsync<MoongateHttpLoginResponse>();

        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(payload, Is.Not.Null);

        http.DefaultRequestHeaders.Authorization = new("Bearer", payload!.AccessToken);

        return http;
    }
}
