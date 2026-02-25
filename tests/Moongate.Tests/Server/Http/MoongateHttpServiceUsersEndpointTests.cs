using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Http;
using Moongate.Server.Http.Data;
using Moongate.Server.Http.Data.Results;
using Moongate.Tests.Server.Http.Support;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server.Http;

public class MoongateHttpServiceUsersEndpointTests
{
    [Test]
    public async Task UsersEndpoint_WhenConfigured_ShouldReturnUsers()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var port = GetRandomPort();

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = false,
                UsersFacade = new TestHttpUsersFacade(
                    _ => Task.FromResult(
                        MoongateHttpOperationResult<IReadOnlyList<MoongateHttpUser>>.Ok(
                            [
                                new MoongateHttpUser
                                {
                                    AccountId = "1",
                                    Username = "admin",
                                    Email = "admin@moongate.local",
                                    Role = "Administrator",
                                    IsLocked = false,
                                    CreatedUtc = DateTime.UtcNow,
                                    LastLoginUtc = DateTime.UtcNow,
                                    CharacterCount = 1
                                }
                            ]
                        )
                    ),
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
        var users = new Dictionary<string, MoongateHttpUser>(StringComparer.Ordinal)
        {
            ["10"] = new MoongateHttpUser
            {
                AccountId = "10",
                Username = "seed",
                Email = "seed@moongate.local",
                Role = "Regular",
                IsLocked = false,
                CreatedUtc = DateTime.UtcNow,
                LastLoginUtc = DateTime.UtcNow,
                CharacterCount = 0
            }
        };

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = false,
                UsersFacade = new TestHttpUsersFacade(
                    _ => Task.FromResult(
                        MoongateHttpOperationResult<IReadOnlyList<MoongateHttpUser>>.Ok([.. users.Values])
                    ),
                    (accountId, _) =>
                    {
                        users.TryGetValue(accountId, out var user);

                        return Task.FromResult(
                            user is null
                                ? MoongateHttpOperationResult<MoongateHttpUser>.NotFound()
                                : MoongateHttpOperationResult<MoongateHttpUser>.Ok(user)
                        );
                    },
                    (request, _) =>
                    {
                        if (users.Values.Any(static u => u.Username.Equals("admin", StringComparison.Ordinal)))
                        {
                            return Task.FromResult(MoongateHttpOperationResult<MoongateHttpUser>.Conflict());
                        }

                        var created = new MoongateHttpUser
                        {
                            AccountId = "11",
                            Username = request.Username,
                            Email = request.Email,
                            Role = request.Role,
                            IsLocked = false,
                            CreatedUtc = DateTime.UtcNow,
                            LastLoginUtc = DateTime.UtcNow,
                            CharacterCount = 0
                        };
                        users[created.AccountId] = created;

                        return Task.FromResult(
                            MoongateHttpOperationResult<MoongateHttpUser>.Created(
                                created,
                                $"/api/users/{created.AccountId}"
                            )
                        );
                    },
                    (accountId, request, _) =>
                    {
                        if (!users.TryGetValue(accountId, out var existing))
                        {
                            return Task.FromResult(MoongateHttpOperationResult<MoongateHttpUser>.NotFound());
                        }

                        var updated = new MoongateHttpUser
                        {
                            AccountId = existing.AccountId,
                            Username = request.Username ?? existing.Username,
                            Email = request.Email ?? existing.Email,
                            Role = request.Role ?? existing.Role,
                            IsLocked = request.IsLocked ?? existing.IsLocked,
                            CreatedUtc = existing.CreatedUtc,
                            LastLoginUtc = existing.LastLoginUtc,
                            CharacterCount = existing.CharacterCount
                        };
                        users[accountId] = updated;

                        return Task.FromResult(MoongateHttpOperationResult<MoongateHttpUser>.Ok(updated));
                    },
                    (accountId, _) => Task.FromResult(
                        users.Remove(accountId)
                            ? MoongateHttpOperationResult<object?>.NoContent()
                            : MoongateHttpOperationResult<object?>.NotFound()
                    )
                )
            }
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

        var service = new MoongateHttpService(
            new()
            {
                DirectoriesConfig = directories,
                Port = port,
                IsOpenApiEnabled = false,
                UsersFacade = new TestHttpUsersFacade(
                    _ => Task.FromResult(MoongateHttpOperationResult<IReadOnlyList<MoongateHttpUser>>.Ok([])),
                    (id, _) => Task.FromResult(
                        id == "42"
                            ? MoongateHttpOperationResult<MoongateHttpUser>.Ok(
                                new MoongateHttpUser
                                {
                                    AccountId = "42",
                                    Username = "test",
                                    Email = "test@moongate.local",
                                    Role = "Regular",
                                    IsLocked = false,
                                    CreatedUtc = DateTime.UtcNow,
                                    LastLoginUtc = DateTime.UtcNow,
                                    CharacterCount = 0
                                }
                            )
                            : MoongateHttpOperationResult<MoongateHttpUser>.NotFound()
                    ),
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
