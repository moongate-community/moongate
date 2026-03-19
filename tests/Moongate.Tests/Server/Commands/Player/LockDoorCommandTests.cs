using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Targeting;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Commands.Player;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Commands.Player;

public sealed class LockDoorCommandTests
{
    private sealed class LockDoorCommandTestGameEventBusService : IGameEventBusService
    {
        public TargetRequestCursorEvent? LastTargetRequestEvent { get; private set; }

        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            _ = cancellationToken;

            if (gameEvent is TargetRequestCursorEvent targetRequestCursorEvent)
            {
                LastTargetRequestEvent = targetRequestCursorEvent;
            }

            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener)
            where TEvent : IGameEvent
            => _ = listener;

        public void TriggerCursorCallback(Serial clickedOnId)
        {
            var packet = new TargetCursorCommandsPacket
            {
                CursorTarget = TargetCursorSelectionType.SelectObject,
                CursorType = TargetCursorType.Neutral,
                ClickedOnId = clickedOnId
            };

            LastTargetRequestEvent!.Value.Callback(new(packet));
        }
    }

    private sealed class LockDoorCommandTestDoorLockService : IDoorLockService
    {
        public Serial LastDoorId { get; private set; }
        public DoorLockResult Result { get; } = new(true, "generated-lock");

        public Task<DoorLockResult> LockDoorAsync(Serial doorId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            LastDoorId = doorId;

            return Task.FromResult(Result);
        }

        public Task<bool> UnlockDoorAsync(Serial doorId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class LockDoorCommandTestItemService : IItemService
    {
        public string? SpawnedTemplateId { get; private set; }
        public Serial LastMoveItemId { get; private set; }
        public Serial LastContainerId { get; private set; }
        public UOItemEntity SpawnedItem { get; } = new() { Id = (Serial)0x40000020u, ItemId = 0x100E, Name = "Key" };
        public UOItemEntity? DoorItem { get; set; }

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromResult(item.Id);

        public Task<bool> DeleteItemAsync(Serial itemId)
            => Task.FromResult(false);

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => Task.FromResult<DropItemToGroundResult?>(null);

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => Task.FromResult(false);

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(DoorItem is not null && DoorItem.Id == itemId ? DoorItem : null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
        {
            LastMoveItemId = itemId;
            LastContainerId = containerId;

            return Task.FromResult(true);
        }

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(false);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
        {
            SpawnedTemplateId = itemTemplateId;

            return Task.FromResult(SpawnedItem);
        }

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((false, (UOItemEntity?)null));

        public Task UpsertItemAsync(UOItemEntity item)
            => Task.CompletedTask;

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;
    }

    private sealed class LockDoorCommandTestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public int Count => _sessions.Count;

        public void Add(GameSession session)
            => _sessions[session.SessionId] = session;

        public void Clear()
            => _sessions.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => _sessions.Values.ToArray();

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
            => _sessions.Remove(sessionId);

        public bool TryGet(long sessionId, out GameSession session)
            => _sessions.TryGetValue(sessionId, out session!);

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            session = _sessions.Values.FirstOrDefault(x => x.CharacterId == characterId)!;

            return session is not null;
        }
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenDoorSelected_ShouldLockDoorAndCreateKeyInBackpack()
    {
        var gameEventBus = new LockDoorCommandTestGameEventBusService();
        var doorLockService = new LockDoorCommandTestDoorLockService();
        var itemService = new LockDoorCommandTestItemService
        {
            DoorItem = new()
            {
                Id = (Serial)0x40000001u,
                Name = "North gate",
                ItemId = 0x0685
            }
        };
        var sessionService = new LockDoorCommandTestGameNetworkSessionService();
        var character = new UOMobileEntity
        {
            Id = (Serial)0x00000010u,
            BackpackId = (Serial)0x40000010u
        };
        var session = new GameSession(
            new(
                new(
                    new(
                        AddressFamily.InterNetwork,
                        SocketType.Stream,
                        ProtocolType.Tcp
                    )
                )
            )
        )
        {
            CharacterId = character.Id,
            Character = character
        };
        sessionService.Add(session);
        var output = new List<string>();
        var command = new LockDoorCommand(gameEventBus, sessionService, doorLockService, itemService);
        var context = new CommandSystemContext(
            ".lock_door",
            [],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);
        gameEventBus.TriggerCursorCallback((Serial)0x40000001u);

        Assert.Multiple(
            () =>
            {
                Assert.That(doorLockService.LastDoorId, Is.EqualTo((Serial)0x40000001u));
                Assert.That(itemService.SpawnedTemplateId, Is.EqualTo("key"));
                Assert.That(itemService.LastMoveItemId, Is.EqualTo(itemService.SpawnedItem.Id));
                Assert.That(itemService.LastContainerId, Is.EqualTo(character.BackpackId));
                Assert.That(
                    itemService.SpawnedItem.TryGetCustomString(ItemCustomParamKeys.Key.LockId, out var lockId)
                        ? lockId
                        : null,
                    Is.EqualTo("generated-lock")
                );
                Assert.That(itemService.SpawnedItem.Name, Is.EqualTo("North gate's key"));
            }
        );
    }
}
