using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Commands.Player;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Commands.Player;

public sealed class AddItemBackpackCommandTests
{
    private sealed class AddItemBackpackTestItemService : IItemService
    {
        public int SpawnCalls { get; private set; }
        public int MoveToContainerCalls { get; private set; }
        public string? LastSpawnTemplateId { get; private set; }
        public Serial LastMoveContainerId { get; private set; }
        public bool MoveToContainerResult { get; set; }
        public UOItemEntity SpawnedItem { get; set; } = new() { Id = (Serial)0x40000100u };

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromResult((Serial)0x40000101u);

        public Task<bool> DeleteItemAsync(Serial itemId)
            => Task.FromResult(true);

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => Task.FromResult<DropItemToGroundResult?>(null);

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => Task.FromResult(true);

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
        {
            _ = itemId;
            _ = position;
            _ = sessionId;
            LastMoveContainerId = containerId;
            MoveToContainerCalls++;

            return Task.FromResult(MoveToContainerResult);
        }

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(true);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
        {
            LastSpawnTemplateId = itemTemplateId;
            SpawnCalls++;

            return Task.FromResult(SpawnedItem);
        }

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((false, (UOItemEntity?)null));

        public Task UpsertItemAsync(UOItemEntity item)
            => Task.CompletedTask;

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;
    }

    private sealed class AddItemBackpackTestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public AddItemBackpackTestGameNetworkSessionService() { }

        public AddItemBackpackTestGameNetworkSessionService(GameSession session)
        {
            _sessions[session.SessionId] = session;
        }

        public int Count => _sessions.Count;

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
            session = _sessions.Values.FirstOrDefault(current => current.CharacterId == characterId)!;

            return session is not null;
        }
    }

    private sealed class AddItemBackpackTestCharacterService : ICharacterService
    {
        public UOMobileEntity? CharacterToReturn { get; set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => Task.CompletedTask;

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult((Serial)0x00000003u);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
        {
            _ = characterId;

            return Task.FromResult(CharacterToReturn);
        }

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenArgumentsAreInvalid_ShouldPrintUsage()
    {
        var command = new AddItemBackpackCommand(
            new AddItemBackpackTestItemService(),
            new AddItemBackpackTestGameNetworkSessionService(),
            new AddItemBackpackTestCharacterService()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "add_item_backpack",
            [],
            CommandSourceType.InGame,
            1,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(output[^1], Is.EqualTo("Usage: .add_item_backpack <templateId>"));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenBackpackMissing_ShouldPrintFailure()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000002u
        };
        var characterService = new AddItemBackpackTestCharacterService
        {
            CharacterToReturn = new()
            {
                Id = session.CharacterId,
                BackpackId = Serial.Zero
            }
        };
        var command = new AddItemBackpackCommand(
            new AddItemBackpackTestItemService(),
            new AddItemBackpackTestGameNetworkSessionService(session),
            characterService
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "add_item_backpack brick",
            ["brick"],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(output[^1], Is.EqualTo("Failed to add item: backpack not found."));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenSessionIsMissing_ShouldPrintFailure()
    {
        var itemService = new AddItemBackpackTestItemService();
        var command = new AddItemBackpackCommand(
            itemService,
            new AddItemBackpackTestGameNetworkSessionService(),
            new AddItemBackpackTestCharacterService()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "add_item_backpack brick",
            ["brick"],
            CommandSourceType.InGame,
            99,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(output[^1], Is.EqualTo("Failed to add item: no active session found."));
                Assert.That(itemService.SpawnCalls, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenSuccessful_ShouldSpawnAndMoveItemToBackpack()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000002u
        };
        var characterService = new AddItemBackpackTestCharacterService
        {
            CharacterToReturn = new()
            {
                Id = session.CharacterId,
                BackpackId = (Serial)0x40000011u
            }
        };
        var itemService = new AddItemBackpackTestItemService
        {
            SpawnedItem = new()
            {
                Id = (Serial)0x40000100u
            },
            MoveToContainerResult = true
        };
        var command = new AddItemBackpackCommand(
            itemService,
            new AddItemBackpackTestGameNetworkSessionService(session),
            characterService
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "add_item_backpack brick",
            ["brick"],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(itemService.SpawnCalls, Is.EqualTo(1));
                Assert.That(itemService.LastSpawnTemplateId, Is.EqualTo("brick"));
                Assert.That(itemService.MoveToContainerCalls, Is.EqualTo(1));
                Assert.That(itemService.LastMoveContainerId, Is.EqualTo((Serial)0x40000011u));
                Assert.That(output[^1], Is.EqualTo("Added 'brick' to backpack."));
            }
        );
    }
}
