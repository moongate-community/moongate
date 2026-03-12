using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Spans;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public class LoginHandlerTests
{
    private sealed class LoginHandlerTestAccountService : IAccountService
    {
        public UOAccountEntity? NextLoginResult { get; set; }

        public Task<bool> CheckAccountExistsAsync(string username)
        {
            _ = username;

            return Task.FromResult(false);
        }

        public Task<UOAccountEntity?> CreateAccountAsync(
            string username,
            string password,
            string email = "",
            AccountType accountType = AccountType.Regular
        )
        {
            _ = username;
            _ = password;
            _ = email;
            _ = accountType;

            return Task.FromResult<UOAccountEntity?>(null);
        }

        public Task<bool> DeleteAccountAsync(Serial accountId)
        {
            _ = accountId;

            return Task.FromResult(false);
        }

        public Task<UOAccountEntity?> GetAccountAsync(Serial accountId)
        {
            _ = accountId;

            return Task.FromResult<UOAccountEntity?>(null);
        }

        public Task<IReadOnlyList<UOAccountEntity>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return Task.FromResult<IReadOnlyList<UOAccountEntity>>(Array.Empty<UOAccountEntity>());
        }

        public Task<UOAccountEntity?> LoginAsync(string username, string password)
        {
            _ = username;
            _ = password;

            return Task.FromResult(NextLoginResult);
        }

        public Task<UOAccountEntity?> UpdateAccountAsync(
            Serial accountId,
            string? username = null,
            string? password = null,
            string? email = null,
            AccountType? accountType = null,
            bool? isLocked = null,
            bool clearRecoveryCode = false,
            CancellationToken cancellationToken = default
        )
        {
            _ = accountId;
            _ = username;
            _ = password;
            _ = email;
            _ = accountType;
            _ = isLocked;
            _ = clearRecoveryCode;
            _ = cancellationToken;

            return Task.FromResult<UOAccountEntity?>(null);
        }
    }

    private sealed class LoginHandlerTestCharacterService : ICharacterService
    {
        public int GetCharactersForAccountCalls { get; private set; }

        public List<UOMobileEntity> CharactersForAccount { get; set; } = [];

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
        {
            _ = accountId;
            _ = characterId;

            return Task.FromResult(false);
        }

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
        {
            _ = characterId;
            _ = shirtHue;
            _ = pantsHue;

            return Task.CompletedTask;
        }

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
        {
            _ = character;

            return Task.FromResult(Serial.Zero);
        }

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
        {
            _ = character;

            return Task.FromResult<UOItemEntity?>(null);
        }

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
        {
            _ = characterId;

            return Task.FromResult<UOMobileEntity?>(null);
        }

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
        {
            _ = accountId;
            GetCharactersForAccountCalls++;

            return Task.FromResult(CharactersForAccount);
        }

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
        {
            _ = accountId;
            _ = characterId;

            return Task.FromResult(false);
        }
    }

    [Test]
    public async Task HandlePacketAsync_WhenClientVersionPacketIsEmpty_ShouldNotStoreClientVersion()
    {
        var handler = CreateHandler();
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = new ClientVersionPacket();
        Assert.That(packet.TryParse(new byte[] { 0xBD, 0x00, 0x03 }), Is.True);

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(session.ClientVersion, Is.Null);
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenClientVersionPacketIsReceived_ShouldStoreClientVersionInSession()
    {
        var handler = CreateHandler();
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = new ClientVersionPacket();
        Assert.That(packet.TryParse(BuildClientVersionPayload("7.0.61.0", true)), Is.True);

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(session.ClientVersion, Is.Not.Null);
                Assert.That(session.ClientVersion!.Major, Is.EqualTo(7));
                Assert.That(session.ClientVersion.Minor, Is.EqualTo(0));
                Assert.That(session.ClientVersion.Revision, Is.EqualTo(61));
                Assert.That(session.ClientVersion.Patch, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenSelectingCharacterAfterGameLogin_ShouldReuseCachedCharacterList()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var accountService = new LoginHandlerTestAccountService();
        var characterService = new LoginHandlerTestCharacterService
        {
            CharactersForAccount =
            [
                new()
                {
                    Id = (Serial)0x00000042,
                    Name = "TestChar",
                    MapId = 0,
                    Location = new(0, 0, 0)
                }
            ]
        };
        var handler = new LoginHandler(
            queue,
            accountService,
            characterService,
            new NetworkServiceTestGameEventBusService(),
            new(),
            new FakeGameNetworkSessionService()
        );

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            AccountId = (Serial)0x00000001
        };

        var gameLoginPacket = new GameLoginPacket
        {
            AccountName = "admin",
            Password = "admin"
        };
        var loginCharacterPacket = new LoginCharacterPacket
        {
            CharacterName = "TestChar"
        };

        // Simulate authenticated session to reach character-list fetch.
        accountService.NextLoginResult = new()
        {
            Id = (Serial)0x00000001,
            Username = "admin",
            PasswordHash = "x"
        };

        _ = await handler.HandlePacketAsync(session, gameLoginPacket);
        _ = await handler.HandlePacketAsync(session, loginCharacterPacket);

        Assert.That(characterService.GetCharactersForAccountCalls, Is.EqualTo(1));
    }

    private static byte[] BuildClientVersionPayload(string version, bool includeNullTerminator)
    {
        var writer = new SpanWriter(64, true);
        writer.Write((byte)0xBD);
        writer.Write((ushort)0);
        writer.WriteAscii(version);

        if (includeNullTerminator)
        {
            writer.Write((byte)0);
        }

        writer.WritePacketLength();
        var data = writer.ToArray();
        writer.Dispose();

        return data;
    }

    private static LoginHandler CreateHandler()
        => new(
            new BasePacketListenerTestOutgoingPacketQueue(),
            new LoginHandlerTestAccountService(),
            new LoginHandlerTestCharacterService(),
            new NetworkServiceTestGameEventBusService(),
            new(),
            new FakeGameNetworkSessionService()
        );
}
