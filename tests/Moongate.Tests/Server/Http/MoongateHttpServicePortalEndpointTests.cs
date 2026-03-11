using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Http;
using Moongate.Server.Http.Data;
using Moongate.Server.Interfaces.Characters;
using Moongate.Tests.Server.Http.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Http;

public class MoongateHttpServicePortalEndpointTests
{
    [Test]
    public async Task PortalMePutEndpoint_WhenAuthenticated_ShouldUpdateEmail()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetValues<DirectoryType>().Cast<Enum>().ToArray());
        var port = GetRandomPort();

        var account = new UOAccountEntity
        {
            Id = (Serial)7,
            Username = "player_one",
            Email = "player@moongate.test",
            AccountType = AccountType.Regular
        };

        var accountService = new TestAccountService
        {
            LoginAsyncImpl = (username, password) =>
                Task.FromResult<UOAccountEntity?>(username == "player_one" && password == "secret" ? account : null),
            GetAccountAsyncImpl = accountId =>
                Task.FromResult<UOAccountEntity?>(accountId == account.Id ? account : null),
            UpdateAccountAsyncImpl = (accountId, _, _, email, _, _, _) =>
            {
                if (accountId != account.Id || string.IsNullOrWhiteSpace(email))
                {
                    return Task.FromResult<UOAccountEntity?>(null);
                }

                account.Email = email;
                return Task.FromResult<UOAccountEntity?>(account);
            }
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
            accountService,
            characterService: new PortalTestCharacterService()
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var loginResponse = await http.PostAsJsonAsync(
                                    $"http://127.0.0.1:{port}/auth/login",
                                    new MoongateHttpLoginRequest
                                    {
                                        Username = "player_one",
                                        Password = "secret"
                                    }
                                );
            var loginPayload = await loginResponse.Content.ReadFromJsonAsync<MoongateHttpLoginResponse>();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginPayload!.AccessToken);

            var response = await http.PutAsJsonAsync(
                               $"http://127.0.0.1:{port}/api/portal/me",
                               new { email = "updated@moongate.test" }
                           );

            using var payloadDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = payloadDoc.RootElement;

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(root.GetProperty("email").GetString(), Is.EqualTo("updated@moongate.test"));
                    Assert.That(account.Email, Is.EqualTo("updated@moongate.test"));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task PortalMePutEndpoint_WhenEmailIsInvalid_ShouldReturnBadRequest()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetValues<DirectoryType>().Cast<Enum>().ToArray());
        var port = GetRandomPort();

        var account = new UOAccountEntity
        {
            Id = (Serial)7,
            Username = "player_one",
            Email = "player@moongate.test",
            AccountType = AccountType.Regular
        };

        var accountService = new TestAccountService
        {
            LoginAsyncImpl = (username, password) =>
                Task.FromResult<UOAccountEntity?>(username == "player_one" && password == "secret" ? account : null),
            GetAccountAsyncImpl = accountId =>
                Task.FromResult<UOAccountEntity?>(accountId == account.Id ? account : null)
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
            accountService,
            characterService: new PortalTestCharacterService()
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var loginResponse = await http.PostAsJsonAsync(
                                    $"http://127.0.0.1:{port}/auth/login",
                                    new MoongateHttpLoginRequest
                                    {
                                        Username = "player_one",
                                        Password = "secret"
                                    }
                                );
            var loginPayload = await loginResponse.Content.ReadFromJsonAsync<MoongateHttpLoginResponse>();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginPayload!.AccessToken);

            var response = await http.PutAsJsonAsync(
                               $"http://127.0.0.1:{port}/api/portal/me",
                               new { email = "not-an-email" }
                           );

            var body = await response.Content.ReadAsStringAsync();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                    Assert.That(body, Does.Contain("invalid email"));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task PortalMeEndpoint_WhenAuthenticated_ShouldReturnCurrentAccountAndCharacters()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetValues<DirectoryType>().Cast<Enum>().ToArray());
        var port = GetRandomPort();

        var account = new UOAccountEntity
        {
            Id = (Serial)7,
            Username = "player_one",
            Email = "player@moongate.test",
            AccountType = AccountType.Regular
        };

        var accountService = new TestAccountService
        {
            LoginAsyncImpl = (username, password) =>
                Task.FromResult<UOAccountEntity?>(username == "player_one" && password == "secret" ? account : null),
            GetAccountAsyncImpl = accountId =>
                Task.FromResult<UOAccountEntity?>(accountId == account.Id ? account : null)
        };

        var characterService = new PortalTestCharacterService
        {
            Characters =
            [
                new UOMobileEntity
                {
                    Id = (Serial)0x00000031u,
                    Name = "Lilly",
                    MapId = 1,
                    Location = new Point3D(1445, 1692, 0)
                },
                new UOMobileEntity
                {
                    Id = (Serial)0x00000032u,
                    Name = "Orion",
                    MapId = 0,
                    Location = new Point3D(512, 824, 5)
                }
            ]
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
            accountService,
            characterService: characterService
        );

        await service.StartAsync();

        try
        {
            using var http = new HttpClient();
            var loginResponse = await http.PostAsJsonAsync(
                                    $"http://127.0.0.1:{port}/auth/login",
                                    new MoongateHttpLoginRequest
                                    {
                                        Username = "player_one",
                                        Password = "secret"
                                    }
                                );
            var loginPayload = await loginResponse.Content.ReadFromJsonAsync<MoongateHttpLoginResponse>();

            var anonymousResponse = await http.GetAsync($"http://127.0.0.1:{port}/api/portal/me");

            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginPayload!.AccessToken);

            var authenticatedResponse = await http.GetAsync($"http://127.0.0.1:{port}/api/portal/me");
            using var payloadDoc = JsonDocument.Parse(await authenticatedResponse.Content.ReadAsStringAsync());
            var root = payloadDoc.RootElement;

            Assert.Multiple(
                () =>
                {
                    Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                    Assert.That(authenticatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(root.GetProperty("accountId").GetString(), Is.EqualTo("7"));
                    Assert.That(root.GetProperty("username").GetString(), Is.EqualTo("player_one"));
                    Assert.That(root.GetProperty("email").GetString(), Is.EqualTo("player@moongate.test"));
                    Assert.That(root.GetProperty("accountType").GetString(), Is.EqualTo(nameof(AccountType.Regular)));
                    Assert.That(root.GetProperty("characters").GetArrayLength(), Is.EqualTo(2));
                    Assert.That(root.GetProperty("characters")[0].GetProperty("name").GetString(), Is.EqualTo("Lilly"));
                    Assert.That(root.GetProperty("characters")[0].GetProperty("mapId").GetInt32(), Is.EqualTo(1));
                    Assert.That(root.GetProperty("characters")[0].GetProperty("mapName").GetString(), Is.EqualTo("Trammel"));
                    Assert.That(root.GetProperty("characters")[0].GetProperty("x").GetInt32(), Is.EqualTo(1445));
                    Assert.That(root.GetProperty("characters")[0].GetProperty("y").GetInt32(), Is.EqualTo(1692));
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

    private sealed class PortalTestCharacterService : ICharacterService
    {
        public List<UOMobileEntity> Characters { get; init; } = [];

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId) => Task.FromResult(false);

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue) => Task.CompletedTask;

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character) => Task.FromResult(character.Id);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character) => Task.FromResult<UOItemEntity?>(null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId) =>
            Task.FromResult<UOMobileEntity?>(Characters.FirstOrDefault(character => character.Id == characterId));

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId) =>
            Task.FromResult(Characters.ToList());

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId) => Task.FromResult(false);
    }
}
