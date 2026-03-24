using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Player;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Network.Spans;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public sealed class CharacterProfileHandlerTests
{
    private sealed class TestAccountService : IAccountService
    {
        public Dictionary<Serial, UOAccountEntity> AccountsById { get; } = [];

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
            AccountsById.TryGetValue(accountId, out var account);

            return Task.FromResult(account);
        }

        public Task<IReadOnlyList<UOAccountEntity>> GetAccountsAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return Task.FromResult<IReadOnlyList<UOAccountEntity>>(AccountsById.Values.ToArray());
        }

        public Task<UOAccountEntity?> LoginAsync(string username, string password)
        {
            _ = username;
            _ = password;

            return Task.FromResult<UOAccountEntity?>(null);
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

    private sealed class TestMobileService : IMobileService
    {
        public Dictionary<Serial, UOMobileEntity> MobilesById { get; } = [];

        public UOMobileEntity? LastUpdatedMobile { get; private set; }

        public int CreateOrUpdateCalls { get; private set; }

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            CreateOrUpdateCalls++;
            LastUpdatedMobile = mobile;
            MobilesById[mobile.Id] = mobile;

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = id;
            _ = cancellationToken;

            return Task.FromResult(false);
        }

        public Task<bool> DismountAsync(Serial riderId, CancellationToken cancellationToken = default)
        {
            _ = riderId;
            _ = cancellationToken;

            return Task.FromResult(false);
        }

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            MobilesById.TryGetValue(id, out var mobile);

            return Task.FromResult(mobile);
        }

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
            int mapId,
            int sectorX,
            int sectorY,
            CancellationToken cancellationToken = default
        )
        {
            _ = mapId;
            _ = sectorX;
            _ = sectorY;
            _ = cancellationToken;

            return Task.FromResult(new List<UOMobileEntity>());
        }

        public Task<UOMobileEntity> SpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

        public Task<bool> TryMountAsync(Serial riderId, Serial mountId, CancellationToken cancellationToken = default)
        {
            _ = riderId;
            _ = mountId;
            _ = cancellationToken;

            return Task.FromResult(false);
        }

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult((false, (UOMobileEntity?)null));
    }

    [Test]
    public async Task HandlePacketAsync_DisplaySelf_ShouldEnqueueProfileWithAccountAgeFooter()
    {
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var accounts = new TestAccountService();
        var mobiles = new TestMobileService();
        var handler = new CharacterProfileHandler(outgoing, accounts, mobiles);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var self = CreatePlayer((Serial)0x100, "Tommy", (Serial)0x500, new(100, 100, 0));
        self.Fame = 10000;
        self.Karma = 10000;
        self.Title = "the brave";
        self.Gender = GenderType.Male;
        self.SetCustomString("profile_body", "Hello Britannia");
        accounts.AccountsById[self.AccountId] = new()
        {
            Id = self.AccountId,
            Username = "admin",
            PasswordHash = "hash",
            CreatedUtc = DateTime.UtcNow.AddDays(-3).AddMinutes(-5)
        };

        var session = new GameSession(new(client))
        {
            AccountId = self.AccountId,
            CharacterId = self.Id,
            Character = self
        };

        var packet = ParseDisplayPacket(self.Id);

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(outgoing.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<DisplayCharacterProfilePacket>());

                var displayPacket = (DisplayCharacterProfilePacket)outbound.Packet;
                Assert.That(displayPacket.TargetId, Is.EqualTo(self.Id));
                Assert.That(displayPacket.Header, Is.EqualTo("The Glorious Lord Tommy the brave"));
                Assert.That(displayPacket.Body, Is.EqualTo("Hello Britannia"));
                Assert.That(displayPacket.Footer, Is.EqualTo("This account is 3 days old."));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_DisplayOtherVisiblePlayer_ShouldEnqueueProfileWithoutSelfFooter()
    {
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var accounts = new TestAccountService();
        var mobiles = new TestMobileService();
        var handler = new CharacterProfileHandler(outgoing, accounts, mobiles);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var self = CreatePlayer((Serial)0x100, "Viewer", (Serial)0x500, new(100, 100, 0));
        var target = CreatePlayer((Serial)0x101, "Target", (Serial)0x501, new(105, 100, 0));
        target.Fame = 4000;
        target.Karma = 1000;
        target.Title = "the tailor";
        target.SetCustomString("profile_body", "Target body");
        mobiles.MobilesById[target.Id] = target;

        var session = new GameSession(new(client))
        {
            AccountId = self.AccountId,
            CharacterId = self.Id,
            Character = self
        };

        var packet = ParseDisplayPacket(target.Id);

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(outgoing.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<DisplayCharacterProfilePacket>());

                var displayPacket = (DisplayCharacterProfilePacket)outbound.Packet;
                Assert.That(displayPacket.TargetId, Is.EqualTo(target.Id));
                Assert.That(displayPacket.Header, Is.EqualTo("Target the tailor"));
                Assert.That(displayPacket.Body, Is.EqualTo("Target body"));
                Assert.That(displayPacket.Footer, Is.Empty);
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_DisplayLockedSelf_ShouldUseSerialZero()
    {
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var accounts = new TestAccountService();
        var mobiles = new TestMobileService();
        var handler = new CharacterProfileHandler(outgoing, accounts, mobiles);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var self = CreatePlayer((Serial)0x100, "Tommy", (Serial)0x500, new(100, 100, 0));
        self.SetCustomBoolean("profile_locked", true);
        self.SetCustomString("profile_body", "Locked");
        var session = new GameSession(new(client))
        {
            AccountId = self.AccountId,
            CharacterId = self.Id,
            Character = self
        };

        var handled = await handler.HandlePacketAsync(session, ParseDisplayPacket(self.Id));

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(outgoing.TryDequeue(out var outbound), Is.True);
                var displayPacket = (DisplayCharacterProfilePacket)outbound.Packet;
                Assert.That(displayPacket.TargetId, Is.EqualTo(Serial.Zero));
                Assert.That(displayPacket.Footer, Is.EqualTo("Your profile has been locked."));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_DisplayLockedOtherAsGameMaster_ShouldShowLockedFooter()
    {
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var accounts = new TestAccountService();
        var mobiles = new TestMobileService();
        var handler = new CharacterProfileHandler(outgoing, accounts, mobiles);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var self = CreatePlayer((Serial)0x100, "Viewer", (Serial)0x500, new(100, 100, 0));
        var target = CreatePlayer((Serial)0x101, "Target", (Serial)0x501, new(101, 100, 0));
        target.SetCustomBoolean("profile_locked", true);
        mobiles.MobilesById[target.Id] = target;

        var session = new GameSession(new(client))
        {
            AccountId = self.AccountId,
            AccountType = AccountType.GameMaster,
            CharacterId = self.Id,
            Character = self
        };

        var handled = await handler.HandlePacketAsync(session, ParseDisplayPacket(target.Id));

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(outgoing.TryDequeue(out var outbound), Is.True);
                var displayPacket = (DisplayCharacterProfilePacket)outbound.Packet;
                Assert.That(displayPacket.TargetId, Is.EqualTo(target.Id));
                Assert.That(displayPacket.Footer, Is.EqualTo("This profile has been locked."));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_DisplayOutOfRangeTarget_ShouldIgnoreRequest()
    {
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var accounts = new TestAccountService();
        var mobiles = new TestMobileService();
        var handler = new CharacterProfileHandler(outgoing, accounts, mobiles);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var self = CreatePlayer((Serial)0x100, "Viewer", (Serial)0x500, new(100, 100, 0));
        var target = CreatePlayer((Serial)0x101, "Target", (Serial)0x501, new(130, 100, 0));
        mobiles.MobilesById[target.Id] = target;
        var session = new GameSession(new(client))
        {
            AccountId = self.AccountId,
            CharacterId = self.Id,
            Character = self
        };

        var handled = await handler.HandlePacketAsync(session, ParseDisplayPacket(target.Id));

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(outgoing.CurrentQueueDepth, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_DisplayHiddenTarget_ShouldIgnoreRequest()
    {
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var accounts = new TestAccountService();
        var mobiles = new TestMobileService();
        var handler = new CharacterProfileHandler(outgoing, accounts, mobiles);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var self = CreatePlayer((Serial)0x100, "Viewer", (Serial)0x500, new(100, 100, 0));
        var target = CreatePlayer((Serial)0x101, "Target", (Serial)0x501, new(101, 100, 0));
        target.IsHidden = true;
        mobiles.MobilesById[target.Id] = target;
        var session = new GameSession(new(client))
        {
            AccountId = self.AccountId,
            CharacterId = self.Id,
            Character = self
        };

        var handled = await handler.HandlePacketAsync(session, ParseDisplayPacket(target.Id));

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(outgoing.CurrentQueueDepth, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_UpdateSelf_ShouldPersistSanitizedProfileBody()
    {
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var accounts = new TestAccountService();
        var mobiles = new TestMobileService();
        var handler = new CharacterProfileHandler(outgoing, accounts, mobiles);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var self = CreatePlayer((Serial)0x100, "Viewer", (Serial)0x500, new(100, 100, 0));
        mobiles.MobilesById[self.Id] = self;
        var session = new GameSession(new(client))
        {
            AccountId = self.AccountId,
            CharacterId = self.Id,
            Character = self
        };

        var handled = await handler.HandlePacketAsync(session, ParseUpdatePacket(self.Id, "Hello\r\nBritannia\0"));

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(self.TryGetCustomString("profile_body", out var body), Is.True);
                Assert.That(body, Is.EqualTo("Hello\nBritannia"));
                Assert.That(mobiles.CreateOrUpdateCalls, Is.EqualTo(1));
                Assert.That(mobiles.LastUpdatedMobile, Is.SameAs(self));
                Assert.That(outgoing.CurrentQueueDepth, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_UpdateLockedSelf_ShouldRejectAndKeepBody()
    {
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var accounts = new TestAccountService();
        var mobiles = new TestMobileService();
        var handler = new CharacterProfileHandler(outgoing, accounts, mobiles);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var self = CreatePlayer((Serial)0x100, "Viewer", (Serial)0x500, new(100, 100, 0));
        self.SetCustomString("profile_body", "Existing");
        self.SetCustomBoolean("profile_locked", true);
        mobiles.MobilesById[self.Id] = self;
        var session = new GameSession(new(client))
        {
            AccountId = self.AccountId,
            CharacterId = self.Id,
            Character = self
        };

        var handled = await handler.HandlePacketAsync(session, ParseUpdatePacket(self.Id, "New body"));

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(self.TryGetCustomString("profile_body", out var body), Is.True);
                Assert.That(body, Is.EqualTo("Existing"));
                Assert.That(mobiles.CreateOrUpdateCalls, Is.EqualTo(0));
                Assert.That(outgoing.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<UnicodeSpeechMessagePacket>());
                Assert.That(((UnicodeSpeechMessagePacket)outbound.Packet).Text, Is.EqualTo("Your profile is locked. You may not change it."));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_DisplaySelfWithoutAccount_ShouldReturnEmptyFooter()
    {
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var accounts = new TestAccountService();
        var mobiles = new TestMobileService();
        var handler = new CharacterProfileHandler(outgoing, accounts, mobiles);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var self = CreatePlayer((Serial)0x100, "Tommy", (Serial)0x500, new(100, 100, 0));
        var session = new GameSession(new(client))
        {
            AccountId = self.AccountId,
            CharacterId = self.Id,
            Character = self
        };

        var handled = await handler.HandlePacketAsync(session, ParseDisplayPacket(self.Id));

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(outgoing.TryDequeue(out var outbound), Is.True);
                var displayPacket = (DisplayCharacterProfilePacket)outbound.Packet;
                Assert.That(displayPacket.Footer, Is.Empty);
            }
        );
    }

    private static UOMobileEntity CreatePlayer(Serial id, string name, Serial accountId, Point3D location)
        => new()
        {
            Id = id,
            AccountId = accountId,
            Name = name,
            IsPlayer = true,
            IsAlive = true,
            MapId = 0,
            Location = location,
            Gender = GenderType.Male
        };

    private static RequestCharProfilePacket ParseDisplayPacket(Serial targetId)
    {
        var packet = new RequestCharProfilePacket();
        var writer = new SpanWriter(8, true);
        writer.Write((byte)0xB8);
        writer.Write((ushort)8);
        writer.Write((byte)0x00);
        writer.Write(targetId.Value);

        Assert.That(packet.TryParse(writer.ToArray()), Is.True);
        writer.Dispose();

        return packet;
    }

    private static RequestCharProfilePacket ParseUpdatePacket(Serial targetId, string profileText)
    {
        var packet = new RequestCharProfilePacket();
        var writer = new SpanWriter(64 + (profileText.Length * 2), true);
        var length = 1 + 2 + 1 + 4 + 2 + 2 + (profileText.Length * 2);

        writer.Write((byte)0xB8);
        writer.Write((ushort)length);
        writer.Write((byte)0x01);
        writer.Write(targetId.Value);
        writer.Write((ushort)0x0001);
        writer.Write((ushort)profileText.Length);
        writer.WriteBigUni(profileText, profileText.Length);

        Assert.That(packet.TryParse(writer.ToArray()), Is.True);
        writer.Dispose();

        return packet;
    }
}
