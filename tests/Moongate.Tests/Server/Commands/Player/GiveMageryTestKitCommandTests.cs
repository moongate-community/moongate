using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Commands.Player;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Magic;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Services.Magic;
using Moongate.Server.Services.Magic.Spells.Magery.First;
using Moongate.Server.Types.Magic;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Commands.Player;

public sealed class GiveMageryTestKitCommandTests
{
    private sealed class GiveMageryTestKitTestItemService : IItemService
    {
        private uint _nextSerial = 0x40000000u;

        public List<UOItemEntity> SpawnedItems { get; } = [];

        public List<(Serial ItemId, Serial ContainerId)> MoveOperations { get; } = [];

        public List<UOItemEntity> UpsertedItems { get; } = [];

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromResult(item.Id);

        public Task<bool> DeleteItemAsync(Serial itemId)
            => Task.FromResult(true);

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
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
            _ = position;
            _ = sessionId;
            MoveOperations.Add((itemId, containerId));

            return Task.FromResult(true);
        }

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(true);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
        {
            var item = new UOItemEntity
            {
                Id = (Serial)_nextSerial++,
                Amount = 1,
                IsStackable = !string.Equals(itemTemplateId, "spellbook", StringComparison.OrdinalIgnoreCase)
            };
            item.SetCustomString(ItemCustomParamKeys.Item.TemplateId, itemTemplateId);
            SpawnedItems.Add(item);

            return Task.FromResult(item);
        }

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((false, (UOItemEntity?)null));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            UpsertedItems.Add(item);

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
        {
            UpsertedItems.AddRange(items);

            return Task.CompletedTask;
        }
    }

    private sealed class GiveMageryTestKitTestCharacterService : ICharacterService
    {
        public UOMobileEntity? Character { get; set; }

        public UOItemEntity? Backpack { get; set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => Task.CompletedTask;

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult(character.Id);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
        {
            _ = character;

            return Task.FromResult(Backpack);
        }

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
        {
            _ = characterId;

            return Task.FromResult(Character);
        }

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);
    }

    private sealed class GiveMageryTestKitTestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public GiveMageryTestKitTestGameNetworkSessionService(GameSession session)
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

    [Test]
    public async Task ExecuteCommandAsync_WhenPlayerHasNoSpellbook_ShouldCreateFullMageryKit()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var character = new UOMobileEntity
        {
            Id = (Serial)0x00002001u,
            IsPlayer = true,
            Intelligence = 25,
            Mana = 10
        };
        character.RecalculateMaxStats();
        var backpack = new UOItemEntity { Id = (Serial)0x40001000u };
        var session = new GameSession(new(client))
        {
            CharacterId = character.Id,
            Character = character
        };
        var sessionService = new GiveMageryTestKitTestGameNetworkSessionService(session);
        var characterService = new GiveMageryTestKitTestCharacterService
        {
            Character = character,
            Backpack = backpack
        };
        var itemService = new GiveMageryTestKitTestItemService();
        var spellbookService = new SpellbookService(characterService);
        var registry = new SpellRegistry();
        registry.Register(new HealSpell());
        registry.Register(new MagicArrowSpell());
        var command = new GiveMageryTestKitCommand(itemService, sessionService, characterService, spellbookService, registry);
        var output = new List<string>();
        var context = new CommandSystemContext(
            "give_magery_test_kit",
            [],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message),
            character.Id
        );

        await command.ExecuteCommandAsync(context);

        var spellbook = itemService.SpawnedItems.Single(item => item.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out var templateId) && templateId == "spellbook");
        var spellbookData = spellbookService.GetData(spellbook);
        var reagentTemplateIds = itemService.SpawnedItems
            .Where(item => item.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out var templateId) && templateId != "spellbook")
            .Select(GetTemplateId)
            .OrderBy(templateId => templateId)
            .ToArray();

        Assert.Multiple(
            () =>
            {
                Assert.That(character.Skills[UOSkillName.Magery].Value, Is.EqualTo(1000));
                Assert.That(character.Intelligence, Is.EqualTo(100));
                Assert.That(character.MaxMana, Is.EqualTo(100));
                Assert.That(character.Mana, Is.EqualTo(100));
                Assert.That(spellbookData.HasSpell(SpellIds.Magery.First.Heal), Is.True);
                Assert.That(spellbookData.HasSpell(SpellIds.Magery.First.MagicArrow), Is.True);
                Assert.That(reagentTemplateIds, Is.EqualTo(new[] { "garlic", "ginseng", "sulfurous_ash" }));
                Assert.That(itemService.MoveOperations, Has.Count.EqualTo(4));
                Assert.That(itemService.MoveOperations.All(move => move.ContainerId == backpack.Id), Is.True);
                Assert.That(output[^1], Does.Contain("Prepared magery test kit"));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenPlayerAlreadyHasSpellbook_ShouldReuseIt()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var character = new UOMobileEntity
        {
            Id = (Serial)0x00002002u,
            IsPlayer = true,
            Intelligence = 110,
            Mana = 15
        };
        character.RecalculateMaxStats();
        var backpack = new UOItemEntity { Id = (Serial)0x40002000u };
        var existingSpellbook = new UOItemEntity { Id = (Serial)0x40002001u };
        existingSpellbook.SetCustomString(ItemCustomParamKeys.Item.TemplateId, "spellbook");
        backpack.AddItem(existingSpellbook, Point2D.Zero);
        var session = new GameSession(new(client))
        {
            CharacterId = character.Id,
            Character = character
        };
        var sessionService = new GiveMageryTestKitTestGameNetworkSessionService(session);
        var characterService = new GiveMageryTestKitTestCharacterService
        {
            Character = character,
            Backpack = backpack
        };
        var itemService = new GiveMageryTestKitTestItemService();
        var spellbookService = new SpellbookService(characterService);
        var registry = new SpellRegistry();
        registry.Register(new HealSpell());
        registry.Register(new MagicArrowSpell());
        var command = new GiveMageryTestKitCommand(itemService, sessionService, characterService, spellbookService, registry);
        var output = new List<string>();
        var context = new CommandSystemContext(
            "give_magery_test_kit",
            [],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message),
            character.Id
        );

        await command.ExecuteCommandAsync(context);

        var spellbookData = spellbookService.GetData(existingSpellbook);

        Assert.Multiple(
            () =>
            {
                Assert.That(itemService.SpawnedItems.Any(item => item.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out var templateId) && templateId == "spellbook"), Is.False);
                Assert.That(spellbookData.HasSpell(SpellIds.Magery.First.Heal), Is.True);
                Assert.That(spellbookData.HasSpell(SpellIds.Magery.First.MagicArrow), Is.True);
                Assert.That(character.Intelligence, Is.EqualTo(110));
                Assert.That(character.MaxMana, Is.EqualTo(110));
                Assert.That(character.Mana, Is.EqualTo(110));
                Assert.That(output[^1], Does.Contain("Prepared magery test kit"));
            }
        );
    }

    private static string GetTemplateId(UOItemEntity item)
    {
        item.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out var templateId);

        return templateId ?? string.Empty;
    }
}
