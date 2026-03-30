using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Internal.Cursors;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Magic;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Data.Session;
using Moongate.Network.Packets.Incoming.Targeting;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Services.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.Network.Packets.Interfaces;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Services.Magic;

[TestFixture]
public sealed class MagicServiceTests
{
    private RecordingTimerService _timerService = null!;
    private RecordingGameEventBusService _gameEventBusService = null!;
    private FakeCharacterService _characterService = null!;
    private FakeGameNetworkSessionService _gameNetworkSessionService = null!;
    private MagicServiceTestSpatialWorldService _spatialWorldService = null!;
    private RecordingPlayerTargetService _playerTargetService = null!;
    private FakeItemService _itemService = null!;
    private AllowAllSpellbookService _spellbookService = null!;
    private SpellRegistry _spellRegistry = null!;
    private MagicService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _timerService = new RecordingTimerService();
        _gameEventBusService = new RecordingGameEventBusService();
        _characterService = new FakeCharacterService();
        _gameNetworkSessionService = new FakeGameNetworkSessionService();
        _spatialWorldService = new MagicServiceTestSpatialWorldService();
        _playerTargetService = new RecordingPlayerTargetService();
        _itemService = new FakeItemService();
        _spellbookService = new AllowAllSpellbookService();
        _spellRegistry = new SpellRegistry();
        _service = new MagicService(
            _timerService,
            _gameEventBusService,
            _characterService,
            _gameNetworkSessionService,
            _spatialWorldService,
            _playerTargetService,
            _itemService,
            _spellbookService,
            _spellRegistry
        );
    }

    [Test]
    public async Task TryCastAsync_DeadCaster_ReturnsFalse()
    {
        _spellRegistry.Register(new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [], [])));
        var caster = CreateCaster(isAlive: false, mana: 50);

        var result = await _service.TryCastAsync(caster, StubSpellId);

        Assert.That(result, Is.False);
        Assert.That(_service.IsCasting(caster.Id), Is.False);
    }

    [Test]
    public async Task TryCastAsync_UnregisteredSpell_ReturnsFalse()
    {
        var caster = CreateCaster(isAlive: true, mana: 50);

        var result = await _service.TryCastAsync(caster, StubSpellId);

        Assert.That(result, Is.False);
        Assert.That(_service.IsCasting(caster.Id), Is.False);
    }

    [Test]
    public async Task TryCastAsync_InsufficientMana_ReturnsFalse()
    {
        _spellRegistry.Register(new StubSpell(StubSpellId, 10, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [], [])));
        var caster = CreateCaster(isAlive: true, mana: 9);

        var result = await _service.TryCastAsync(caster, StubSpellId);

        Assert.That(result, Is.False);
        Assert.That(_service.IsCasting(caster.Id), Is.False);
    }

    [Test]
    public async Task TryCastAsync_WithoutAvailableSpellbook_ReturnsFalse()
    {
        _spellRegistry.Register(new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [], [])));
        _spellbookService.AllowAllSpells = false;
        var caster = CreateCaster(isAlive: true, mana: 50);

        var result = await _service.TryCastAsync(caster, StubSpellId);

        Assert.That(result, Is.False);
        Assert.That(_service.IsCasting(caster.Id), Is.False);
    }

    [Test]
    public async Task TryCastAsync_WithAvailableSpellbook_ReturnsTrue()
    {
        _spellRegistry.Register(new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [], [])));
        _spellbookService.AllowAllSpells = true;
        var caster = CreateCaster(isAlive: true, mana: 50);

        var result = await _service.TryCastAsync(caster, StubSpellId);

        Assert.That(result, Is.True);
        Assert.That(_service.IsCasting(caster.Id), Is.True);
    }

    [Test]
    public async Task TryCastAsync_MissingRequiredReagents_ReturnsFalse()
    {
        _spellRegistry.Register(
            new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [ReagentType.Garlic], [1]))
        );
        var caster = CreateCaster(isAlive: true, mana: 50);

        var result = await _service.TryCastAsync(caster, StubSpellId);

        Assert.That(result, Is.False);
        Assert.That(_service.IsCasting(caster.Id), Is.False);
        Assert.That(_characterService.GetBackpackWithItemsCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task TryCastAsync_ValidSpell_StartsCastingAndRegistersTimer()
    {
        _spellRegistry.Register(new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1.5), new("Heal", "In Mani", [], [])));
        var caster = CreateCaster(isAlive: true, mana: 50);

        var result = await _service.TryCastAsync(caster, StubSpellId);

        Assert.That(result, Is.True);
        Assert.That(_service.IsCasting(caster.Id), Is.True);
        Assert.That(caster.Mana, Is.EqualTo(46));
        Assert.That(_timerService.RegisteredTimers, Has.Count.EqualTo(1));
        Assert.That(_timerService.RegisteredTimers[0].Name, Is.EqualTo($"spell_cast_{caster.Id}_{StubSpellId}"));
        Assert.That(_timerService.RegisteredTimers[0].Interval, Is.EqualTo(TimeSpan.FromSeconds(1.5)));
    }

    [Test]
    public async Task OnCastTimerExpiredAsync_WhenUntargetedSpellCompletes_ConsumesRequiredReagents()
    {
        var spell = new RecordingEffectSpell(
            StubSpellId,
            4,
            TimeSpan.FromSeconds(1),
            new("Heal", "In Mani", [ReagentType.Garlic], [2]),
            SpellTargetingType.None
        );
        _spellRegistry.Register(spell);
        var caster = CreateCaster(isAlive: true, mana: 50);
        var garlicStack = CreateReagent((Serial)0x40000011u, "garlic", 5);
        _characterService.Backpack = CreateBackpack(garlicStack);
        _gameNetworkSessionService.Add(CreateSession(caster));

        _ = await _service.TryCastAsync(caster, StubSpellId);
        await _service.OnCastTimerExpiredAsync(caster.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(spell.ApplyCalls, Is.EqualTo(1));
                Assert.That(garlicStack.Amount, Is.EqualTo(3));
                Assert.That(_itemService.UpsertedItemIds, Is.EqualTo(new[] { garlicStack.Id }));
                Assert.That(_itemService.DeletedItemIds, Is.Empty);
            }
        );
    }

    [Test]
    public async Task TrySetTarget_WhenSpellIsSequencing_ConsumesReagentsOnlyAfterTargetIsBound()
    {
        var spell = new RecordingEffectSpell(
            StubSpellId,
            4,
            TimeSpan.FromSeconds(1),
            new("Magic Arrow", "In Por Ylem", [ReagentType.BlackPearl], [1]),
            SpellTargetingType.RequiredMobile
        );
        _spellRegistry.Register(spell);
        var caster = CreateCaster(isAlive: true, mana: 50);
        var target = new UOMobileEntity
        {
            Id = (Serial)0x00000002u,
            IsAlive = true,
            MapId = 1,
            Location = new Point3D(101, 100, 0)
        };
        var blackPearlStack = CreateReagent((Serial)0x40000012u, "black_pearl", 2);
        var pouch = CreateContainer((Serial)0x40000013u, blackPearlStack);
        _characterService.Backpack = CreateBackpack(pouch);
        caster.MapId = 1;
        caster.Location = new Point3D(100, 100, 0);
        _spatialWorldService.AddMobile(caster);
        _spatialWorldService.AddMobile(target);
        _gameNetworkSessionService.Add(CreateSession(caster));

        _ = await _service.TryCastAsync(caster, StubSpellId);
        await _service.OnCastTimerExpiredAsync(caster.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(spell.ApplyCalls, Is.EqualTo(0));
                Assert.That(blackPearlStack.Amount, Is.EqualTo(2));
                Assert.That(_itemService.UpsertedItemIds, Is.Empty);
                Assert.That(_itemService.DeletedItemIds, Is.Empty);
            }
        );

        _playerTargetService.InvokeObjectResponse(target.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(spell.ApplyCalls, Is.EqualTo(1));
                Assert.That(blackPearlStack.Amount, Is.EqualTo(1));
                Assert.That(_itemService.UpsertedItemIds, Is.EqualTo(new[] { blackPearlStack.Id }));
                Assert.That(_itemService.DeletedItemIds, Is.Empty);
                Assert.That(target.TryGetCustomInteger("magic.effect_applied", out var marker), Is.True);
                Assert.That(marker, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public async Task TryCastAsync_WhileAlreadyCasting_ReturnsFalse()
    {
        _spellRegistry.Register(new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [], [])));
        var caster = CreateCaster(isAlive: true, mana: 50);

        _ = await _service.TryCastAsync(caster, StubSpellId);

        var result = await _service.TryCastAsync(caster, StubSpellId);

        Assert.That(result, Is.False);
        Assert.That(_timerService.RegisteredTimers, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Interrupt_ActiveCast_DoesNotConsumeReagents()
    {
        _spellRegistry.Register(
            new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [ReagentType.Garlic], [1]))
        );
        var caster = CreateCaster(isAlive: true, mana: 50);
        var garlicStack = CreateReagent((Serial)0x40000014u, "garlic", 3);
        _characterService.Backpack = CreateBackpack(garlicStack);

        _ = await _service.TryCastAsync(caster, StubSpellId);
        _service.Interrupt(caster.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(garlicStack.Amount, Is.EqualTo(3));
                Assert.That(_itemService.UpsertedItemIds, Is.Empty);
                Assert.That(_itemService.DeletedItemIds, Is.Empty);
            }
        );
    }

    [Test]
    public async Task Interrupt_ActiveCast_ClearsCastStateAndUnregistersTimer()
    {
        _spellRegistry.Register(new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [], [])));
        var caster = CreateCaster(isAlive: true, mana: 50);

        _ = await _service.TryCastAsync(caster, StubSpellId);

        _service.Interrupt(caster.Id);

        Assert.That(_service.IsCasting(caster.Id), Is.False);
        Assert.That(_timerService.UnregisteredTimerIds, Has.Count.EqualTo(1));
        Assert.That(_timerService.UnregisteredTimerIds[0], Is.EqualTo($"spell_cast_{caster.Id}_{StubSpellId}"));
    }

    [Test]
    public async Task OnCastTimerExpiredAsync_WhenReagentsDisappearBeforeCompletion_SkipsSpellEffect()
    {
        var spell = new RecordingEffectSpell(
            StubSpellId,
            4,
            TimeSpan.FromSeconds(1),
            new("Heal", "In Mani", [ReagentType.Garlic], [1]),
            SpellTargetingType.None
        );
        _spellRegistry.Register(spell);
        var caster = CreateCaster(isAlive: true, mana: 50);
        var garlicStack = CreateReagent((Serial)0x40000015u, "garlic", 1);
        var backpack = CreateBackpack(garlicStack);
        _characterService.Backpack = backpack;
        _gameNetworkSessionService.Add(CreateSession(caster));

        _ = await _service.TryCastAsync(caster, StubSpellId);
        _ = backpack.RemoveItem(garlicStack.Id);

        await _service.OnCastTimerExpiredAsync(caster.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(spell.ApplyCalls, Is.EqualTo(0));
                Assert.That(caster.TryGetCustomInteger("magic.effect_applied", out _), Is.False);
                Assert.That(_itemService.UpsertedItemIds, Is.Empty);
                Assert.That(_itemService.DeletedItemIds, Is.Empty);
            }
        );
    }

    [Test]
    public async Task OnCastTimerExpiredAsync_ActiveCast_ClearsCastState()
    {
        _spellRegistry.Register(new StubSpell(StubSpellId, 4, TimeSpan.FromSeconds(1), new("Heal", "In Mani", [], [])));
        var caster = CreateCaster(isAlive: true, mana: 50);
        _gameNetworkSessionService.Add(CreateSession(caster));

        _ = await _service.TryCastAsync(caster, StubSpellId);

        await _service.OnCastTimerExpiredAsync(caster.Id);

        Assert.That(_service.IsCasting(caster.Id), Is.False);
    }

    [Test]
    public async Task OnCastTimerExpiredAsync_WhenSessionCharacterExists_AppliesSpellEffect()
    {
        var spell = new RecordingEffectSpell(
            StubSpellId,
            4,
            TimeSpan.FromSeconds(1),
            new("Heal", "In Mani", [], []),
            SpellTargetingType.None
        );
        _spellRegistry.Register(spell);
        var caster = CreateCaster(isAlive: true, mana: 50);
        _gameNetworkSessionService.Add(CreateSession(caster));

        _ = await _service.TryCastAsync(caster, StubSpellId);

        await _service.OnCastTimerExpiredAsync(caster.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(_service.IsCasting(caster.Id), Is.False);
                Assert.That(spell.ApplyCalls, Is.EqualTo(1));
                Assert.That(caster.TryGetCustomInteger("magic.effect_applied", out var marker), Is.True);
                Assert.That(marker, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public async Task TrySetTarget_WhenActiveCastMatchesSpell_BindsPendingTarget()
    {
        _spellRegistry.Register(
            new StubSpell(
                StubSpellId,
                4,
                TimeSpan.FromSeconds(1),
                new("Magic Arrow", "In Por Ylem", [], []),
                SpellTargetingType.RequiredMobile
            )
        );
        var caster = CreateCaster(isAlive: true, mana: 50);

        _ = await _service.TryCastAsync(caster, StubSpellId);

        var result = _service.TrySetTarget(caster.Id, StubSpellId, (Serial)0x00000002u);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task OnCastTimerExpiredAsync_WhenTargetIsBound_AppliesSpellEffectToTarget()
    {
        var spell = new RecordingEffectSpell(
            StubSpellId,
            4,
            TimeSpan.FromSeconds(1),
            new("Magic Arrow", "In Por Ylem", [], []),
            SpellTargetingType.RequiredMobile
        );
        _spellRegistry.Register(spell);
        var caster = CreateCaster(isAlive: true, mana: 50);
        var target = new UOMobileEntity
        {
            Id = (Serial)0x00000002u,
            IsAlive = true
        };
        _gameNetworkSessionService.Add(CreateSession(caster));
        _gameNetworkSessionService.Add(CreateSession(target));

        _ = await _service.TryCastAsync(caster, StubSpellId);
        _service.TrySetTarget(caster.Id, StubSpellId, target.Id);

        await _service.OnCastTimerExpiredAsync(caster.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(spell.ApplyCalls, Is.EqualTo(1));
                Assert.That(target.TryGetCustomInteger("magic.effect_applied", out var targetMarker), Is.True);
                Assert.That(targetMarker, Is.EqualTo(1));
                Assert.That(caster.TryGetCustomInteger("magic.effect_applied", out _), Is.False);
            }
        );
    }

    [Test]
    public async Task OnCastTimerExpiredAsync_WhenNpcCasterExistsInSpatialWorld_AppliesSpellEffect()
    {
        var spell = new RecordingEffectSpell(
            StubSpellId,
            4,
            TimeSpan.FromSeconds(1),
            new("Reactive Armor", "Flam Sanct", [], []),
            SpellTargetingType.None
        );
        _spellRegistry.Register(spell);
        var caster = CreateCaster(isAlive: true, mana: 50);
        caster.MapId = 1;
        caster.Location = new Point3D(100, 100, 0);
        _spatialWorldService.AddMobile(caster);

        _ = await _service.TryCastAsync(caster, StubSpellId);

        await _service.OnCastTimerExpiredAsync(caster.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(spell.ApplyCalls, Is.EqualTo(1));
                Assert.That(caster.TryGetCustomInteger("magic.effect_applied", out var marker), Is.True);
                Assert.That(marker, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public async Task OnCastTimerExpiredAsync_WhenRequiredTargetSpellHasNoBoundTarget_TransitionsToSequencing()
    {
        _spellRegistry.Register(
            new StubSpell(
                StubSpellId,
                4,
                TimeSpan.FromSeconds(1),
                new("Magic Arrow", "In Por Ylem", [], []),
                SpellTargetingType.RequiredMobile
            )
        );
        var caster = CreateCaster(isAlive: true, mana: 50);
        _gameNetworkSessionService.Add(CreateSession(caster));

        _ = await _service.TryCastAsync(caster, StubSpellId);

        await _service.OnCastTimerExpiredAsync(caster.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(_service.IsCasting(caster.Id), Is.True);
                Assert.That(_timerService.RegisteredTimers.Select(timer => timer.Name), Does.Contain($"spell_sequence_{caster.Id}_{StubSpellId}"));
                Assert.That(_playerTargetService.LastSelectionType, Is.EqualTo(TargetCursorSelectionType.SelectObject));
            }
        );
    }

    [Test]
    public async Task OnCastTimerExpiredAsync_WhenRequiredItemSpellHasNoBoundTarget_RequestsObjectCursor()
    {
        _spellRegistry.Register(
            new StubSpell(
                StubSpellId,
                4,
                TimeSpan.FromSeconds(1),
                new("Gate Travel", "Vas Rel Por", [], []),
                SpellTargetingType.RequiredItem
            )
        );
        var caster = CreateCaster(isAlive: true, mana: 50);
        _gameNetworkSessionService.Add(CreateSession(caster));

        _ = await _service.TryCastAsync(caster, StubSpellId);

        await _service.OnCastTimerExpiredAsync(caster.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(_service.IsCasting(caster.Id), Is.True);
                Assert.That(_timerService.RegisteredTimers.Select(timer => timer.Name), Does.Contain($"spell_sequence_{caster.Id}_{StubSpellId}"));
                Assert.That(_playerTargetService.LastSelectionType, Is.EqualTo(TargetCursorSelectionType.SelectObject));
            }
        );
    }

    [Test]
    public async Task OnCastTimerExpiredAsync_WhenRequiredItemTargetIsProvidedThroughCursor_AppliesSpellEffectToTargetItem()
    {
        var spell = new RecordingExecutionSpell(
            StubSpellId,
            4,
            TimeSpan.FromSeconds(1),
            new("Gate Travel", "Vas Rel Por", [], []),
            SpellTargetingType.RequiredItem
        );
        _spellRegistry.Register(spell);
        var caster = CreateCaster(isAlive: true, mana: 50);
        var targetItem = new UOItemEntity
        {
            Id = (Serial)0x40000010u
        };
        _itemService.Add(targetItem);
        _gameNetworkSessionService.Add(CreateSession(caster));

        _ = await _service.TryCastAsync(caster, StubSpellId);
        await _service.OnCastTimerExpiredAsync(caster.Id);
        _playerTargetService.InvokeObjectResponse(targetItem.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(_service.IsCasting(caster.Id), Is.False);
                Assert.That(spell.ApplyCalls, Is.EqualTo(1));
                Assert.That(targetItem.TryGetCustomInteger("magic.item_effect_applied", out var marker), Is.True);
                Assert.That(marker, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public async Task TrySetTarget_WhenSpellIsSequencing_CompletesEffectAgainstLateTarget()
    {
        var spell = new RecordingEffectSpell(
            StubSpellId,
            4,
            TimeSpan.FromSeconds(1),
            new("Magic Arrow", "In Por Ylem", [], []),
            SpellTargetingType.RequiredMobile
        );
        _spellRegistry.Register(spell);
        var caster = CreateCaster(isAlive: true, mana: 50);
        var target = new UOMobileEntity
        {
            Id = (Serial)0x00000002u,
            IsAlive = true,
            MapId = 1,
            Location = new Point3D(101, 100, 0)
        };
        caster.MapId = 1;
        caster.Location = new Point3D(100, 100, 0);
        _spatialWorldService.AddMobile(caster);
        _spatialWorldService.AddMobile(target);
        _gameNetworkSessionService.Add(CreateSession(caster));

        _ = await _service.TryCastAsync(caster, StubSpellId);
        await _service.OnCastTimerExpiredAsync(caster.Id);

        var result = _service.TrySetTarget(caster.Id, StubSpellId, target.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(_service.IsCasting(caster.Id), Is.False);
                Assert.That(spell.ApplyCalls, Is.EqualTo(1));
                Assert.That(target.TryGetCustomInteger("magic.effect_applied", out var marker), Is.True);
                Assert.That(marker, Is.EqualTo(1));
            }
        );
    }

    private static UOItemEntity CreateBackpack(params UOItemEntity[] items)
    {
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x40000001u,
            ItemId = 0x0E75,
            Amount = 1
        };

        foreach (var item in items)
        {
            backpack.AddItem(item, new Point2D(1, 1));
        }

        return backpack;
    }

    private static UOItemEntity CreateContainer(Serial id, params UOItemEntity[] items)
    {
        var container = new UOItemEntity
        {
            Id = id,
            ItemId = 0x0E76,
            Amount = 1
        };

        foreach (var item in items)
        {
            container.AddItem(item, new Point2D(1, 1));
        }

        return container;
    }

    private static UOItemEntity CreateReagent(Serial id, string templateId, int amount)
    {
        var reagent = new UOItemEntity
        {
            Id = id,
            ItemId = 0x0F8D,
            Amount = amount
        };
        reagent.SetCustomString(ItemCustomParamKeys.Item.TemplateId, templateId);

        return reagent;
    }

    private static UOMobileEntity CreateCaster(bool isAlive, int mana)
    {
        return new UOMobileEntity
        {
            Id = new Serial(1),
            IsAlive = isAlive,
            Mana = mana
        };
    }

    private static GameSession CreateSession(UOMobileEntity character)
    {
        var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        return new GameSession(new(client))
        {
            Character = character,
            CharacterId = character.Id
        };
    }

    private sealed class MagicServiceTestSpatialWorldService : ISpatialWorldService
    {
        private readonly List<MapSector> _sectors = [];

        public void AddMobile(UOMobileEntity mobile)
        {
            var sector = new MapSector(
                mobile.MapId,
                mobile.Location.X >> MapSectorConsts.SectorShift,
                mobile.Location.Y >> MapSectorConsts.SectorShift
            );
            sector.AddEntity(mobile);
            _sectors.Add(sector);
        }

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            _ = item;
            _ = mapId;
        }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => AddMobile(mobile);

        public void AddRegion(JsonRegion region)
        {
            _ = region;
        }

        public Task<int> BroadcastToPlayersAsync(
            IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
        {
            _ = packet;
            _ = mapId;
            _ = location;
            _ = range;
            _ = excludeSessionId;

            return Task.FromResult(0);
        }

        public List<MapSector> GetActiveSectors()
            => [.. _sectors];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
        {
            _ = mapId;
            _ = centerSectorX;
            _ = centerSectorY;
            _ = radius;

            return [];
        }

        public int GetMusic(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;

            return 0;
        }

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;

            return [];
        }

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;

            return [];
        }

        public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapId, GameSession? excludeSession = null)
        {
            _ = location;
            _ = range;
            _ = mapId;
            _ = excludeSession;

            return [];
        }

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
        {
            _ = mapId;
            _ = sectorX;
            _ = sectorY;

            return [];
        }

        public JsonRegion? GetRegionById(int regionId)
        {
            _ = regionId;

            return null;
        }

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;

            return null;
        }

        public SectorSystemStats GetStats()
            => new();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
        {
            _ = item;
            _ = mapId;
            _ = oldLocation;
            _ = newLocation;
        }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
        {
            _ = mobile;
            _ = oldLocation;
            _ = newLocation;
        }

        public void RemoveEntity(Serial serial)
        {
            _ = serial;
        }
    }

    private sealed class RecordingTimerService : ITimerService
    {
        public List<RegisteredTimer> RegisteredTimers { get; } = [];

        public List<string> UnregisteredTimerIds { get; } = [];

        public void ProcessTick()
        {
        }

        public string RegisterTimer(
            string name,
            TimeSpan interval,
            Action callback,
            TimeSpan? delay = null,
            bool repeat = false
        )
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(callback);

            RegisteredTimers.Add(new(name, interval, callback, delay, repeat));

            return name;
        }

        public void UnregisterAllTimers()
        {
            RegisteredTimers.Clear();
        }

        public bool UnregisterTimer(string timerId)
        {
            UnregisteredTimerIds.Add(timerId);

            return true;
        }

        public int UnregisterTimersByName(string name)
        {
            var removed = RegisteredTimers.RemoveAll(timer => timer.Name == name);

            return removed;
        }

        public int UpdateTicksDelta(long timestampMilliseconds)
        {
            _ = timestampMilliseconds;

            return 0;
        }
    }

    private sealed record RegisteredTimer(
        string Name,
        TimeSpan Interval,
        Action Callback,
        TimeSpan? Delay,
        bool Repeat
    );

    private sealed class RecordingGameEventBusService : IGameEventBusService
    {
        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            _ = gameEvent;
            _ = cancellationToken;

            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener)
            where TEvent : IGameEvent
        {
            _ = listener;
        }
    }

    private sealed class AllowAllSpellbookService : ISpellbookService
    {
        public bool AllowAllSpells { get; set; } = true;

        public SpellbookData GetData(UOItemEntity book)
        {
            throw new NotSupportedException();
        }

        public ValueTask<UOItemEntity?> FindSpellbookAsync(
            UOMobileEntity mobile,
            SpellbookType spellbookType,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public ValueTask<bool> MobileHasSpellAsync(
            UOMobileEntity mobile,
            SpellbookType spellbookType,
            int spellId,
            CancellationToken cancellationToken = default
        )
        {
            _ = mobile;
            _ = spellbookType;
            _ = spellId;
            _ = cancellationToken;

            return ValueTask.FromResult(AllowAllSpells);
        }

        public void SetData(UOItemEntity book, SpellbookData data)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeCharacterService : ICharacterService
    {
        public int GetBackpackWithItemsCalls { get; private set; }

        public UOItemEntity? Backpack { get; set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
        {
            throw new NotSupportedException();
        }

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
        {
            throw new NotSupportedException();
        }

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
        {
            throw new NotSupportedException();
        }

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
        {
            ArgumentNullException.ThrowIfNull(character);
            GetBackpackWithItemsCalls++;

            return Task.FromResult(Backpack);
        }

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
        {
            throw new NotSupportedException();
        }

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
        {
            throw new NotSupportedException();
        }

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
        {
            throw new NotSupportedException();
        }

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubSpell : MagerySpellBase
    {
        private readonly int _manaCost;
        private readonly TimeSpan _castDelay;
        private readonly SpellTargetingType _targeting;

        public StubSpell(int spellId, int manaCost, TimeSpan castDelay, SpellInfo info, SpellTargetingType targeting = SpellTargetingType.None)
        {
            SpellId = spellId;
            _manaCost = manaCost;
            _castDelay = castDelay;
            Info = info;
            _targeting = targeting;
        }

        public override int SpellId { get; }

        public override SpellCircleType Circle => SpellCircleType.First;

        public override SpellInfo Info { get; }

        public override SpellTargetingType Targeting => _targeting;

        public override int ManaCost => _manaCost;

        public override TimeSpan CastDelay => _castDelay;

        public override double MinSkill => 0;

        public override double MaxSkill => 100;

        public override void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
        {
            _ = caster;
            _ = target;
        }
    }

    private sealed class RecordingEffectSpell : MagerySpellBase
    {
        private readonly int _manaCost;
        private readonly TimeSpan _castDelay;
        private readonly SpellTargetingType _targeting;

        public RecordingEffectSpell(int spellId, int manaCost, TimeSpan castDelay, SpellInfo info, SpellTargetingType targeting = SpellTargetingType.None)
        {
            SpellId = spellId;
            _manaCost = manaCost;
            _castDelay = castDelay;
            Info = info;
            _targeting = targeting;
        }

        public int ApplyCalls { get; private set; }

        public override int SpellId { get; }

        public override SpellCircleType Circle => SpellCircleType.First;

        public override SpellInfo Info { get; }

        public override SpellTargetingType Targeting => _targeting;

        public override int ManaCost => _manaCost;

        public override TimeSpan CastDelay => _castDelay;

        public override double MinSkill => 0;

        public override double MaxSkill => 100;

        public override void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
        {
            ApplyCalls++;
            (target ?? caster).SetCustomInteger("magic.effect_applied", 1);
        }
    }

    private sealed class RecordingExecutionSpell : MagerySpellBase
    {
        private readonly int _manaCost;
        private readonly TimeSpan _castDelay;
        private readonly SpellTargetingType _targeting;

        public RecordingExecutionSpell(int spellId, int manaCost, TimeSpan castDelay, SpellInfo info, SpellTargetingType targeting)
        {
            SpellId = spellId;
            _manaCost = manaCost;
            _castDelay = castDelay;
            Info = info;
            _targeting = targeting;
        }

        public int ApplyCalls { get; private set; }

        public override int SpellId { get; }

        public override SpellCircleType Circle => SpellCircleType.Seventh;

        public override SpellInfo Info { get; }

        public override SpellTargetingType Targeting => _targeting;

        public override int ManaCost => _manaCost;

        public override TimeSpan CastDelay => _castDelay;

        public override double MinSkill => 0;

        public override double MaxSkill => 100;

        public override void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
        {
            _ = caster;
            _ = target;
        }

        public override ValueTask ApplyEffectAsync(SpellExecutionContext context, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            ApplyCalls++;
            context.TargetItem!.SetCustomInteger("magic.item_effect_applied", 1);

            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingPlayerTargetService : IPlayerTargetService
    {
        private Action<PendingCursorCallback>? _lastCallback;

        public TargetCursorSelectionType LastSelectionType { get; private set; }

        public TargetCursorType LastCursorType { get; private set; }

        public Serial LastCursorId { get; private set; }

        public Task SendCancelTargetCursorAsync(long sessionId, Serial cursorId)
        {
            _ = sessionId;
            _ = cursorId;

            return Task.CompletedTask;
        }

        public Task<Serial> SendTargetCursorAsync(
            long sessionId,
            Action<PendingCursorCallback> callback,
            TargetCursorSelectionType selectionType = TargetCursorSelectionType.SelectLocation,
            TargetCursorType cursorType = TargetCursorType.Neutral
        )
        {
            _ = sessionId;
            _lastCallback = callback;
            LastSelectionType = selectionType;
            LastCursorType = cursorType;
            LastCursorId = (Serial)0x70000001u;

            return Task.FromResult(LastCursorId);
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;

        public void InvokeObjectResponse(Serial clickedOnId)
        {
            _lastCallback?.Invoke(
                new(
                    new TargetCursorCommandsPacket
                    {
                        CursorTarget = TargetCursorSelectionType.SelectObject,
                        CursorId = LastCursorId,
                        CursorType = LastCursorType,
                        ClickedOnId = clickedOnId
                    }
                )
            );
        }

        public void InvokeCancelResponse()
        {
            _lastCallback?.Invoke(
                new(
                    new TargetCursorCommandsPacket
                    {
                        CursorTarget = TargetCursorSelectionType.SelectObject,
                        CursorId = LastCursorId,
                        CursorType = TargetCursorType.CancelCurrentTargeting,
                        ClickedOnId = Serial.Zero
                    }
                )
            );
        }
    }

    private sealed class FakeItemService : IItemService
    {
        private readonly Dictionary<Serial, UOItemEntity> _items = [];

        public List<Serial> DeletedItemIds { get; } = [];

        public List<Serial> UpsertedItemIds { get; } = [];

        public void Add(UOItemEntity item)
            => _items[item.Id] = item;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => throw new NotSupportedException();

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            DeletedItemIds.Add(itemId);

            return Task.FromResult(_items.Remove(itemId));
        }

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(_items.GetValueOrDefault(itemId));

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((_items.ContainsKey(itemId), _items.GetValueOrDefault(itemId)));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            UpsertedItemIds.Add(item.Id);
            _items[item.Id] = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
        {
            foreach (var item in items)
            {
                UpsertedItemIds.Add(item.Id);
                _items[item.Id] = item;
            }

            return Task.CompletedTask;
        }
    }

    private const int StubSpellId = 4;
}
