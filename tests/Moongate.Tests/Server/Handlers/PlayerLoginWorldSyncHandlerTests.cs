using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Characters;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public sealed class PlayerLoginWorldSyncHandlerTests
{
    private sealed class PlayerLoginWorldSyncHandlerTestCharacterService : ICharacterService
    {
        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => throw new NotSupportedException();

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => throw new NotSupportedException();

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => throw new NotSupportedException();

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => throw new NotSupportedException();

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => throw new NotSupportedException();

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => throw new NotSupportedException();
    }

    private sealed class RecordingPlayerLoginWorldSyncService : IPlayerLoginWorldSyncService
    {
        public int CallCount { get; private set; }

        public GameSession? LastSession { get; private set; }

        public UOMobileEntity? LastMobile { get; private set; }

        public Task SyncAsync(
            GameSession session,
            UOMobileEntity mobileEntity,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            CallCount++;
            LastSession = session;
            LastMobile = mobileEntity;

            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task HandleAsync_ShouldInvokeLoginWorldSyncServiceForResolvedCharacter()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            AccountId = (Serial)0x01020304u,
            AccountType = AccountType.Regular
        };
        var character = new UOMobileEntity
        {
            Id = (Serial)0x00005001u,
            IsPlayer = true,
            Name = "tommy",
            MapId = 1,
            Location = new(132, 132, 0)
        };
        session.CharacterId = character.Id;
        session.Character = character;

        var sessions = new FakeGameNetworkSessionService();
        sessions.Add(session);
        var syncService = new RecordingPlayerLoginWorldSyncService();
        var handler = new PlayerLoginWorldSyncHandler(
            sessions,
            new PlayerLoginWorldSyncHandlerTestCharacterService(),
            syncService
        );

        await handler.HandleAsync(
            new(
                session.SessionId,
                session.AccountId,
                character.Id
            )
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(syncService.CallCount, Is.EqualTo(1));
                Assert.That(syncService.LastSession, Is.SameAs(session));
                Assert.That(syncService.LastMobile, Is.SameAs(character));
            }
        );
    }
}
