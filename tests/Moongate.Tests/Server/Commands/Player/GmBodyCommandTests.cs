using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Commands.Player;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Commands.Player;

public sealed class GmBodyCommandTests
{
    private sealed class GmBodyTestItemService : IItemService
    {
        private uint _nextId = 0x40000100;

        public List<string> SpawnedTemplateIds { get; } = [];
        public List<(Serial ItemId, Serial ContainerId)> MovesToContainer { get; } = [];
        public List<(Serial ItemId, Serial MobileId, ItemLayerType Layer)> Equips { get; } = [];

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

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
        {
            Equips.Add((itemId, mobileId, layer));

            return Task.FromResult(true);
        }

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
        {
            _ = position;
            _ = sessionId;
            MovesToContainer.Add((itemId, containerId));

            return Task.FromResult(true);
        }

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(true);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
        {
            SpawnedTemplateIds.Add(itemTemplateId);

            return Task.FromResult(new UOItemEntity { Id = (Serial)_nextId++ });
        }

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((false, (UOItemEntity?)null));

        public Task UpsertItemAsync(UOItemEntity item)
            => Task.CompletedTask;

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;
    }

    private sealed class GmBodyTestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public GmBodyTestGameNetworkSessionService() { }

        public GmBodyTestGameNetworkSessionService(GameSession session)
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

    private sealed class GmBodyTestCharacterService : ICharacterService
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

    private sealed class GmBodyTestItemFactoryService : IItemFactoryService
    {
        public List<string> BagContainer { get; } =
        [
            "gm_hiding_stone",
            "gm_ethereal",
            "gm_staff_orb",
            "gm_staff_ring",
            "gm_fur_boots",
            "gm_skullcap"
        ];

        public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
            => throw new NotSupportedException();

        public UOItemEntity GetNewBackpack()
            => throw new NotSupportedException();

        public bool TryGetItemTemplate(string itemTemplateId, out ItemTemplateDefinition? definition)
        {
            if (!string.Equals(itemTemplateId, "gm_body_bag", StringComparison.OrdinalIgnoreCase))
            {
                definition = null;

                return false;
            }

            definition = new()
            {
                Id = "gm_body_bag",
                Container = [.. BagContainer]
            };

            return true;
        }
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenArgumentsAreProvided_ShouldPrintUsage()
    {
        var command = new GmBodyCommand(
            new GmBodyTestItemService(),
            new GmBodyTestGameNetworkSessionService(),
            new GmBodyTestCharacterService(),
            new GmBodyTestItemFactoryService()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "gm_body now",
            ["now"],
            CommandSourceType.InGame,
            1,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(output[^1], Is.EqualTo("Usage: .gm_body"));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenSuccessful_ShouldCreateKitAndMoveAllItemsToBag()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000002u
        };

        var characterService = new GmBodyTestCharacterService
        {
            CharacterToReturn = new()
            {
                Id = session.CharacterId,
                BackpackId = (Serial)0x40000011u
            }
        };

        var itemService = new GmBodyTestItemService();
        var command = new GmBodyCommand(
            itemService,
            new GmBodyTestGameNetworkSessionService(session),
            characterService,
            new GmBodyTestItemFactoryService()
        );

        var output = new List<string>();
        var context = new CommandSystemContext(
            "gm_body",
            [],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    itemService.SpawnedTemplateIds,
                    Is.EqualTo(
                        new[]
                        {
                            "gm_body_bag",
                            "gm_hiding_stone",
                            "gm_ethereal",
                            "gm_staff_orb",
                            "gm_staff_ring",
                            "gm_fur_boots",
                            "gm_skullcap"
                        }
                    )
                );
                Assert.That(itemService.MovesToContainer.Count, Is.EqualTo(7));
                Assert.That(itemService.MovesToContainer[0].ContainerId, Is.EqualTo((Serial)0x40000011u));
                Assert.That(itemService.Equips.Count, Is.EqualTo(0));
                Assert.That(output[^1], Is.EqualTo("GM body kit added successfully."));
            }
        );
    }
}
