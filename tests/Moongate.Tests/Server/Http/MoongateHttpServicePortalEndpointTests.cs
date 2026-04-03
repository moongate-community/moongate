using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Core.Utils;
using Moongate.Server.Http;
using Moongate.Server.Http.Data;
using Moongate.Server.Interfaces.Characters;
using Moongate.Tests.Server.Http.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Http;

public class MoongateHttpServicePortalEndpointTests
{
    private sealed class PortalTestCharacterService : ICharacterService
    {
        public List<UOMobileEntity> Characters { get; init; } = [];
        public Dictionary<Serial, UOItemEntity> Backpacks { get; } = [];
        public Dictionary<Serial, UOItemEntity> BankBoxes { get; } = [];
        public Dictionary<Serial, UOMobileEntity> CharacterOverrides { get; } = [];

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(false);

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => Task.CompletedTask;

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult(character.Id);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => Task.FromResult(Backpacks.TryGetValue(character.Id, out var backpack) ? backpack : null);

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => Task.FromResult(BankBoxes.TryGetValue(character.Id, out var bankBox) ? bankBox : null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult<UOMobileEntity?>(
                CharacterOverrides.TryGetValue(characterId, out var character)
                    ? character
                    : Characters.FirstOrDefault(candidate => candidate.Id == characterId)
            );

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => Task.FromResult(Characters.ToList());

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(false);
    }

    [Test]
    public async Task PortalCharacterInventoryEndpoint_WhenAuthenticated_ShouldReturnEquippedAndBackpackItems()
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
                                 Task.FromResult(username == "player_one" && password == "secret" ? account : null),
            GetAccountAsyncImpl = accountId =>
                                      Task.FromResult<UOAccountEntity?>(accountId == account.Id ? account : null)
        };

        var backpack = new UOItemEntity
        {
            Id = (Serial)0x40000010u,
            Name = "Backpack",
            ItemId = 0x0E75,
            Amount = 1,
            EquippedMobileId = (Serial)0x00000031u,
            EquippedLayer = ItemLayerType.Backpack
        };

        var sword = new UOItemEntity
        {
            Id = (Serial)0x40000011u,
            Name = "Longsword",
            ItemId = 0x0F60,
            Amount = 1,
            EquippedMobileId = (Serial)0x00000031u,
            EquippedLayer = ItemLayerType.OneHanded
        };

        var bankBox = new UOItemEntity
        {
            Id = (Serial)0x40000020u,
            Name = "Bank Box",
            ItemId = 0x09AB,
            Amount = 1,
            EquippedMobileId = (Serial)0x00000031u,
            EquippedLayer = ItemLayerType.Bank
        };

        var apple = new UOItemEntity
        {
            Id = (Serial)0x40000012u,
            Name = "Apple",
            ItemId = 0x09D0,
            Amount = 3,
            ParentContainerId = backpack.Id
        };

        var pouch = new UOItemEntity
        {
            Id = (Serial)0x40000013u,
            Name = "Pouch",
            ItemId = 0x0E79,
            Amount = 1,
            ParentContainerId = backpack.Id
        };

        var gem = new UOItemEntity
        {
            Id = (Serial)0x40000014u,
            Name = "Sapphire",
            ItemId = 0x0F19,
            Amount = 2,
            ParentContainerId = pouch.Id
        };

        var gold = new UOItemEntity
        {
            Id = (Serial)0x40000021u,
            Name = "Gold",
            ItemId = 0x0EED,
            Amount = 1250,
            ParentContainerId = bankBox.Id
        };

        var chest = new UOItemEntity
        {
            Id = (Serial)0x40000022u,
            Name = "Metal Chest",
            ItemId = 0x0E40,
            Amount = 1,
            ParentContainerId = bankBox.Id
        };

        var deed = new UOItemEntity
        {
            Id = (Serial)0x40000023u,
            Name = "House Deed",
            ItemId = 0x14F0,
            Amount = 1,
            ParentContainerId = chest.Id
        };

        var character = new UOMobileEntity
        {
            Id = (Serial)0x00000031u,
            AccountId = account.Id,
            Name = "Lilly",
            MapId = 1,
            Location = new(1445, 1692, 0),
            BackpackId = backpack.Id
        };
        character.AddEquippedItem(ItemLayerType.Backpack, backpack);
        character.AddEquippedItem(ItemLayerType.OneHanded, sword);
        character.AddEquippedItem(ItemLayerType.Bank, bankBox);

        var characterService = new PortalTestCharacterService
        {
            Characters = [character],
            Backpacks = { [character.Id] = backpack },
            BankBoxes = { [character.Id] = bankBox },
            CharacterOverrides = { [character.Id] = character }
        };
        backpack.AddItem(apple, new(20, 30));
        backpack.AddItem(pouch, new(40, 50));
        pouch.AddItem(gem, new(12, 16));
        bankBox.AddItem(gold, new(22, 24));
        bankBox.AddItem(chest, new(48, 52));
        chest.AddItem(deed, new(10, 12));
        character.HydrateEquipmentRuntime([backpack, sword, bankBox]);

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
            characterService
        );

        await service.StartAsync();

        try
        {
            using var http = await LoginPortalClientAsync(port, "player_one", "secret");

            var response =
                await http.GetAsync($"http://127.0.0.1:{port}/api/portal/characters/{character.Id.Value}/inventory");
            using var payloadDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = payloadDoc.RootElement;

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(root.GetProperty("characterId").GetString(), Is.EqualTo(character.Id.Value.ToString()));
                    Assert.That(root.GetProperty("characterName").GetString(), Is.EqualTo("Lilly"));
                    Assert.That(root.GetProperty("items").GetArrayLength(), Is.EqualTo(5));
                    Assert.That(
                        root.GetProperty("items")[0].GetProperty("location").GetString(),
                        Is.EqualTo("Equipped: Backpack")
                    );
                    Assert.That(
                        root.GetProperty("items")[1].GetProperty("location").GetString(),
                        Is.EqualTo("Equipped: OneHanded")
                    );
                    Assert.That(root.GetProperty("items")[2].GetProperty("location").GetString(), Is.EqualTo("Backpack"));
                    Assert.That(root.GetProperty("items")[3].GetProperty("location").GetString(), Is.EqualTo("Backpack"));
                    Assert.That(
                        root.GetProperty("items")[4].GetProperty("location").GetString(),
                        Is.EqualTo("Container: Pouch")
                    );
                    Assert.That(
                        root.GetProperty("items")[4].GetProperty("imageUrl").GetString(),
                        Does.Contain("/api/item-templates/by-item-id/")
                    );
                    Assert.That(root.GetProperty("bankItems").GetArrayLength(), Is.EqualTo(3));
                    Assert.That(root.GetProperty("bankItems")[0].GetProperty("location").GetString(), Is.EqualTo("Bank"));
                    Assert.That(root.GetProperty("bankItems")[1].GetProperty("location").GetString(), Is.EqualTo("Bank"));
                    Assert.That(
                        root.GetProperty("bankItems")[2].GetProperty("location").GetString(),
                        Is.EqualTo("Container: Metal Chest")
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
    public async Task PortalCharacterInventoryEndpoint_WhenCharacterDoesNotBelongToAccount_ShouldReturnNotFound()
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
                                 Task.FromResult(username == "player_one" && password == "secret" ? account : null),
            GetAccountAsyncImpl = accountId =>
                                      Task.FromResult<UOAccountEntity?>(accountId == account.Id ? account : null)
        };

        var foreignCharacter = new UOMobileEntity
        {
            Id = (Serial)0x00000077u,
            AccountId = (Serial)99,
            Name = "Foreign"
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
            new PortalTestCharacterService
            {
                Characters = [foreignCharacter],
                CharacterOverrides = { [foreignCharacter.Id] = foreignCharacter }
            }
        );

        await service.StartAsync();

        try
        {
            using var http = await LoginPortalClientAsync(port, "player_one", "secret");

            var response = await http.GetAsync(
                               $"http://127.0.0.1:{port}/api/portal/characters/{foreignCharacter.Id.Value}/inventory"
                           );

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
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
                                 Task.FromResult(username == "player_one" && password == "secret" ? account : null),
            GetAccountAsyncImpl = accountId =>
                                      Task.FromResult<UOAccountEntity?>(accountId == account.Id ? account : null)
        };

        var characterService = new PortalTestCharacterService
        {
            Characters =
            [
                new()
                {
                    Id = (Serial)0x00000031u,
                    Name = "Lilly",
                    MapId = 1,
                    Location = new(1445, 1692, 0)
                },
                new()
                {
                    Id = (Serial)0x00000032u,
                    Name = "Orion",
                    MapId = 0,
                    Location = new(512, 824, 5)
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
            characterService
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

            http.DefaultRequestHeaders.Authorization = new("Bearer", loginPayload!.AccessToken);

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

    [Test]
    public async Task PortalMePasswordPutEndpoint_WhenAdminWithoutCurrentPassword_ShouldSucceed()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetValues<DirectoryType>().Cast<Enum>().ToArray());
        var port = GetRandomPort();

        var account = new UOAccountEntity
        {
            Id = (Serial)7,
            Username = "player_one",
            Email = "player@moongate.test",
            AccountType = AccountType.Administrator,
            PasswordHash = HashUtils.HashPassword("secret")
        };

        var accountService = new TestAccountService
        {
            LoginAsyncImpl = (username, password) =>
                                 Task.FromResult(username == "player_one" && password == "secret" ? account : null),
            GetAccountAsyncImpl = accountId =>
                                      Task.FromResult<UOAccountEntity?>(accountId == account.Id ? account : null),
            UpdateAccountAsyncImpl = (accountId, _, password, _, _, _, _, _, _) =>
                                     {
                                         if (accountId != account.Id || string.IsNullOrWhiteSpace(password))
                                         {
                                             return Task.FromResult<UOAccountEntity?>(null);
                                         }

                                         account.PasswordHash = HashUtils.HashPassword(password);

                                         return Task.FromResult<UOAccountEntity?>(account);
                                     }
        };

        var service = CreatePortalHttpService(directories, port, accountService);

        await service.StartAsync();

        try
        {
            using var http = await LoginPortalClientAsync(port, "player_one", "secret");

            var response = await http.PutAsJsonAsync(
                               $"http://127.0.0.1:{port}/api/portal/me/password",
                               new
                               {
                                   currentPassword = "",
                                   newPassword = "new-secret",
                                   confirmPassword = "new-secret"
                               }
                           );

            var body = await response.Content.ReadAsStringAsync();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
                    Assert.That(HashUtils.VerifyPassword("new-secret", account.PasswordHash), Is.True);
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task PortalMePasswordPutEndpoint_WhenConfirmationDoesNotMatch_ShouldReturnBadRequest()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetValues<DirectoryType>().Cast<Enum>().ToArray());
        var port = GetRandomPort();

        var account = new UOAccountEntity
        {
            Id = (Serial)7,
            Username = "player_one",
            Email = "player@moongate.test",
            AccountType = AccountType.Regular,
            PasswordHash = HashUtils.HashPassword("secret")
        };

        var accountService = new TestAccountService
        {
            LoginAsyncImpl = (username, password) =>
                                 Task.FromResult(username == "player_one" && password == "secret" ? account : null),
            GetAccountAsyncImpl = accountId =>
                                      Task.FromResult<UOAccountEntity?>(accountId == account.Id ? account : null)
        };

        var service = CreatePortalHttpService(directories, port, accountService);

        await service.StartAsync();

        try
        {
            using var http = await LoginPortalClientAsync(port, "player_one", "secret");

            var response = await http.PutAsJsonAsync(
                               $"http://127.0.0.1:{port}/api/portal/me/password",
                               new
                               {
                                   currentPassword = "secret",
                                   newPassword = "new-secret",
                                   confirmPassword = "other-secret"
                               }
                           );

            var body = await response.Content.ReadAsStringAsync();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                    Assert.That(body, Does.Contain("confirm"));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task PortalMePasswordPutEndpoint_WhenRegularAndCurrentPasswordIsWrong_ShouldReturnBadRequest()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetValues<DirectoryType>().Cast<Enum>().ToArray());
        var port = GetRandomPort();

        var account = new UOAccountEntity
        {
            Id = (Serial)7,
            Username = "player_one",
            Email = "player@moongate.test",
            AccountType = AccountType.Regular,
            PasswordHash = HashUtils.HashPassword("secret")
        };

        var accountService = new TestAccountService
        {
            LoginAsyncImpl = (username, password) =>
                                 Task.FromResult(username == "player_one" && password == "secret" ? account : null),
            GetAccountAsyncImpl = accountId =>
                                      Task.FromResult<UOAccountEntity?>(accountId == account.Id ? account : null)
        };

        var service = CreatePortalHttpService(directories, port, accountService);

        await service.StartAsync();

        try
        {
            using var http = await LoginPortalClientAsync(port, "player_one", "secret");

            var response = await http.PutAsJsonAsync(
                               $"http://127.0.0.1:{port}/api/portal/me/password",
                               new
                               {
                                   currentPassword = "wrong-secret",
                                   newPassword = "new-secret",
                                   confirmPassword = "new-secret"
                               }
                           );

            var body = await response.Content.ReadAsStringAsync();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                    Assert.That(body, Does.Contain("current password"));
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Test]
    public async Task
        PortalMePasswordPutEndpoint_WhenRegularAndCurrentPasswordMatches_ShouldUpdatePasswordAndClearRecoveryCode()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetValues<DirectoryType>().Cast<Enum>().ToArray());
        var port = GetRandomPort();

        var account = new UOAccountEntity
        {
            Id = (Serial)7,
            Username = "player_one",
            Email = "player@moongate.test",
            AccountType = AccountType.Regular,
            PasswordHash = HashUtils.HashPassword("secret"),
            RecoveryCode = "recover-me"
        };

        var accountService = new TestAccountService
        {
            LoginAsyncImpl = (username, password) =>
                                 Task.FromResult(username == "player_one" && password == "secret" ? account : null),
            GetAccountAsyncImpl = accountId =>
                                      Task.FromResult<UOAccountEntity?>(accountId == account.Id ? account : null),
            UpdateAccountAsyncImpl = (accountId, _, password, _, _, _, _, clearRecoveryCode, _) =>
                                     {
                                         if (accountId != account.Id || string.IsNullOrWhiteSpace(password))
                                         {
                                             return Task.FromResult<UOAccountEntity?>(null);
                                         }

                                         account.PasswordHash = HashUtils.HashPassword(password);

                                         if (clearRecoveryCode)
                                         {
                                             account.RecoveryCode = null;
                                         }

                                         return Task.FromResult<UOAccountEntity?>(account);
                                     }
        };

        var service = CreatePortalHttpService(directories, port, accountService);

        await service.StartAsync();

        try
        {
            using var http = await LoginPortalClientAsync(port, "player_one", "secret");

            var response = await http.PutAsJsonAsync(
                               $"http://127.0.0.1:{port}/api/portal/me/password",
                               new
                               {
                                   currentPassword = "secret",
                                   newPassword = "new-secret",
                                   confirmPassword = "new-secret"
                               }
                           );

            var body = await response.Content.ReadAsStringAsync();

            Assert.Multiple(
                () =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
                    Assert.That(HashUtils.VerifyPassword("new-secret", account.PasswordHash), Is.True);
                    Assert.That(account.RecoveryCode, Is.Null);
                }
            );
        }
        finally
        {
            await service.StopAsync();
        }
    }

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
                                 Task.FromResult(username == "player_one" && password == "secret" ? account : null),
            GetAccountAsyncImpl = accountId =>
                                      Task.FromResult<UOAccountEntity?>(accountId == account.Id ? account : null),
            UpdateAccountAsyncImpl = (accountId, _, _, email, _, _, _, _, _) =>
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
            new PortalTestCharacterService()
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
            http.DefaultRequestHeaders.Authorization = new("Bearer", loginPayload!.AccessToken);

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
                                 Task.FromResult(username == "player_one" && password == "secret" ? account : null),
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
            new PortalTestCharacterService()
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
            http.DefaultRequestHeaders.Authorization = new("Bearer", loginPayload!.AccessToken);

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

    private static MoongateHttpService CreatePortalHttpService(
        DirectoriesConfig directories,
        int port,
        TestAccountService accountService
    )
        => new(
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
            new PortalTestCharacterService()
        );

    private static int GetRandomPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;

        return endpoint.Port;
    }

    private static async Task<HttpClient> LoginPortalClientAsync(int port, string username, string password)
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
