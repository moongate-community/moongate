using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Descriptors;
using Moongate.Server.Data.Interaction;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Data.Internal.Templates;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("mobile", "Provides helpers to resolve mobiles from scripts.")]

/// <summary>
/// Exposes mobile lookup helpers to Lua scripts.
/// </summary>
public sealed class MobileModule
{
    private const int MaxTemplateSearchPageSize = 50;
    private static bool _isLuaMobileProxyTypeRegistered;
    private static bool _isLuaItemProxyTypeRegistered;
    private readonly ICharacterService _characterService;
    private readonly ISpeechService _speechService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly ISpatialWorldService? _spatialWorldService;
    private readonly IMovementValidationService? _movementValidationService;
    private readonly IPathfindingService? _pathfindingService;
    private readonly IGameEventBusService? _gameEventBusService;
    private readonly IBackgroundJobService? _backgroundJobService;
    private readonly IMobileService? _mobileService;
    private readonly IOutgoingPacketQueue? _outgoingPacketQueue;
    private readonly IItemService? _itemService;
    private readonly ISkillGainService? _skillGainService;
    private readonly IMobileTemplateService? _mobileTemplateService;
    private readonly Func<double> _skillCheckRollProvider;

    public MobileModule(
        ICharacterService characterService,
        ISpeechService speechService,
        IGameNetworkSessionService gameNetworkSessionService,
        ISpatialWorldService? spatialWorldService = null,
        IMovementValidationService? movementValidationService = null,
        IPathfindingService? pathfindingService = null,
        IGameEventBusService? gameEventBusService = null,
        IBackgroundJobService? backgroundJobService = null,
        IMobileService? mobileService = null,
        IOutgoingPacketQueue? outgoingPacketQueue = null,
        IItemService? itemService = null,
        ISkillGainService? skillGainService = null,
        IMobileTemplateService? mobileTemplateService = null,
        Func<double>? skillCheckRollProvider = null
    )
    {
        _characterService = characterService;
        _speechService = speechService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _spatialWorldService = spatialWorldService;
        _movementValidationService = movementValidationService;
        _pathfindingService = pathfindingService;
        _gameEventBusService = gameEventBusService;
        _backgroundJobService = backgroundJobService;
        _mobileService = mobileService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _itemService = itemService;
        _skillGainService = skillGainService;
        _mobileTemplateService = mobileTemplateService;
        _skillCheckRollProvider = skillCheckRollProvider ?? Random.Shared.NextDouble;
    }

    [ScriptFunction("dismount", "Attempts to dismount the rider from the current mount.")]
    public bool Dismount(uint riderId)
    {
        if (riderId == 0 || _mobileService is null)
        {
            return false;
        }

        var dismounted = _mobileService.DismountAsync((Serial)riderId).GetAwaiter().GetResult();

        if (dismounted)
        {
            RefreshMountedSession((Serial)riderId, Serial.Zero, false);
        }

        return dismounted;
    }

    [ScriptFunction("get", "Gets a mobile reference by character id, or nil when not found.")]
    public LuaMobileProxy? Get(uint characterId)
    {
        if (characterId == 0)
        {
            return null;
        }

        return CreateLuaMobileProxy(ResolveMobile((Serial)characterId));
    }

    [ScriptFunction("get_backpack", "Gets the backpack item reference for a character id, or nil when unavailable.")]
    public LuaItemProxy? GetBackpack(uint characterId)
    {
        var mobile = ResolveMobile((Serial)characterId);

        if (mobile is null)
        {
            return null;
        }

        return CreateLuaItemProxy(TryResolveBackpack(mobile));
    }

    [ScriptFunction("get_skill", "Gets the displayed base skill value for a character id.")]
    public double GetSkill(uint characterId, string skillName)
    {
        var mobile = ResolveMobile((Serial)characterId);

        if (mobile is null || !TryResolveSkillName(skillName, out var resolvedSkill))
        {
            return 0;
        }

        var skill = mobile.GetSkill(resolvedSkill);

        return skill is null ? 0 : skill.Base / 10.0;
    }

    [ScriptFunction("get_weapon", "Gets the equipped weapon reference for a character id, or nil when unavailable.")]
    public LuaItemProxy? GetWeapon(uint characterId)
    {
        var mobile = ResolveMobile((Serial)characterId);

        if (mobile is null)
        {
            return null;
        }

        return CreateLuaItemProxy(TryResolveWeapon(mobile));
    }

    [ScriptFunction("teleport", "Teleports a character id to the provided map and world coordinates.")]
    public bool Teleport(uint characterId, int mapId, int x, int y, int z)
    {
        var mobile = ResolveMobile((Serial)characterId);

        if (mobile is null)
        {
            return false;
        }

        var proxy = CreateLuaMobileProxy(mobile);

        return proxy is not null && proxy.Teleport(mapId, x, y, z);
    }

    [ScriptFunction("check_skill", "Checks a skill against a min/max range and applies gain rules.")]
    public bool CheckSkill(uint characterId, string skillName, double minSkill, double maxSkill, uint targetId = 0)
    {
        if (_skillGainService is null)
        {
            return false;
        }

        var mobile = ResolveMobile((Serial)characterId);

        if (mobile is null || !TryResolveSkillName(skillName, out var resolvedSkill))
        {
            return false;
        }

        var skill = mobile.GetSkill(resolvedSkill);

        if (skill is null)
        {
            return false;
        }

        var currentValue = skill.Value / 10.0;

        if (currentValue < minSkill)
        {
            return false;
        }

        if (currentValue >= maxSkill || minSkill >= maxSkill)
        {
            return true;
        }

        var successChance = Math.Clamp((currentValue - minSkill) / (maxSkill - minSkill), 0.0, 1.0);
        var wasSuccessful = successChance >= _skillCheckRollProvider();
        _ = _skillGainService.TryGain(
            mobile,
            resolvedSkill,
            successChance,
            wasSuccessful,
            new SkillGainContext(mobile.Location, targetId == 0 ? null : (Serial)targetId)
        );

        return wasSuccessful;
    }

    [ScriptFunction("consume_item", "Consumes a matching item id from equipped quivers first, then backpack.")]
    public bool ConsumeItem(uint characterId, int itemId, int amount = 1)
    {
        if (_itemService is null || characterId == 0 || itemId <= 0 || amount <= 0)
        {
            return false;
        }

        var mobile = ResolveMobile((Serial)characterId);

        if (mobile is null)
        {
            return false;
        }

        var sources = GetConsumableSources(mobile);

        if (CountMatchingItems(sources, itemId) < amount)
        {
            return false;
        }

        for (var i = 0; i < amount; i++)
        {
            if (!TryConsumeFromSources(sources, itemId))
            {
                return false;
            }
        }

        return true;
    }

    [ScriptFunction("add_item_to_backpack", "Spawns an item template and places it in the target backpack.")]
    public LuaItemProxy? AddItemToBackpack(uint characterId, string itemTemplateId, int amount = 1)
    {
        if (_itemService is null || characterId == 0 || string.IsNullOrWhiteSpace(itemTemplateId) || amount <= 0)
        {
            return null;
        }

        var mobile = ResolveMobile((Serial)characterId);

        if (mobile is null)
        {
            return null;
        }

        var backpackId = ResolveBackpackId(mobile);

        if (backpackId == Serial.Zero)
        {
            return null;
        }

        var item = _itemService.SpawnFromTemplateAsync(itemTemplateId.Trim()).GetAwaiter().GetResult();

        if (amount > 1)
        {
            if (!item.IsStackable)
            {
                return null;
            }

            item.Amount = amount;
            _itemService.UpsertItemAsync(item).GetAwaiter().GetResult();
        }

        var moved = _itemService.MoveItemToContainerAsync(item.Id, backpackId, new(1, 1)).GetAwaiter().GetResult();

        return moved ? CreateLuaItemProxy(item) : null;
    }

    [ScriptFunction("spawn", "Spawns a mobile template at world position { x, y, z, map_id }.")]
    public LuaMobileProxy? Spawn(string mobileTemplateId, Table? position)
    {
        if (_mobileService is null ||
            string.IsNullOrWhiteSpace(mobileTemplateId) ||
            !TryParsePosition(position, out var location, out var mapId))
        {
            return null;
        }

        RegisterLuaTypeIfNeeded();
        var mobile = _mobileService.SpawnFromTemplateAsync(mobileTemplateId.Trim(), location, mapId)
                                   .GetAwaiter()
                                   .GetResult();

        return new(
            mobile,
            _speechService,
            _gameNetworkSessionService,
            _spatialWorldService,
            _movementValidationService,
            _pathfindingService,
            _gameEventBusService,
            _backgroundJobService
        );
    }

    [ScriptFunction("search_templates", "Searches mobile templates for GM tools and returns paged results.")]
    public Table SearchTemplates(string query, int page = 1, int pageSize = 20)
    {
        var results = new Table(null);

        if (_mobileTemplateService is null)
        {
            return results;
        }

        var normalizedQuery = query?.Trim() ?? string.Empty;
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxTemplateSearchPageSize);
        var matches = SearchTemplateDefinitions(_mobileTemplateService.GetAll(), normalizedQuery);
        var paged = matches.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize);
        var index = 1;

        foreach (var match in paged)
        {
            var entry = new Table(null)
            {
                ["template_id"] = match.TemplateId,
                ["display_name"] = match.DisplayName
            };
            results[index++] = entry;
        }

        return results;
    }

    [ScriptFunction("try_mount", "Attempts to mount the rider on the target mount creature.")]
    public bool TryMount(uint riderId, uint mountId)
    {
        if (riderId == 0 || mountId == 0 || _mobileService is null)
        {
            return false;
        }

        var riderSerial = (Serial)riderId;
        var mountSerial = (Serial)mountId;
        var rider = ResolveRiderForMount(riderSerial);
        var mount = TryResolveRuntimeMobile(mountSerial) ??
                    _mobileService.GetAsync(mountSerial).GetAwaiter().GetResult();

        if (rider is not null)
        {
            _mobileService.CreateOrUpdateAsync(rider).GetAwaiter().GetResult();
        }

        if (mount is not null)
        {
            _mobileService.CreateOrUpdateAsync(mount).GetAwaiter().GetResult();
        }

        var mounted = _mobileService.TryMountAsync(riderSerial, mountSerial).GetAwaiter().GetResult();

        if (mounted)
        {
            RefreshMountedSession(riderSerial, mountSerial, true);
        }

        return mounted;
    }

    private void RefreshMountedSession(Serial riderId, Serial mountId, bool isMounted)
    {
        if (!_gameNetworkSessionService.TryGetByCharacterId(riderId, out var session) || session.Character is null)
        {
            return;
        }

        var rider = TryResolveRuntimeMobile(riderId) ??
                    _mobileService?.GetAsync(riderId).GetAwaiter().GetResult() ??
                    session.Character;
        var mount = mountId == Serial.Zero
                        ? null
                        : TryResolveRuntimeMobile(mountId) ??
                          _mobileService?.GetAsync(mountId).GetAwaiter().GetResult();

        if (_outgoingPacketQueue is null)
        {
            return;
        }

        MountedSelfRefreshHelper.Refresh(session, _outgoingPacketQueue, rider, mount, isMounted);
    }

    private static void RegisterLuaTypeIfNeeded()
    {
        if (_isLuaMobileProxyTypeRegistered)
        {
            RegisterLuaItemTypeIfNeeded();

            return;
        }

        var type = typeof(LuaMobileProxy);
        UserData.RegisterType(type, new GenericUserDataDescriptor(type));
        _isLuaMobileProxyTypeRegistered = true;
        RegisterLuaItemTypeIfNeeded();
    }

    private static void RegisterLuaItemTypeIfNeeded()
    {
        if (_isLuaItemProxyTypeRegistered)
        {
            return;
        }

        var type = typeof(LuaItemProxy);
        UserData.RegisterType(type, new GenericUserDataDescriptor(type));
        _isLuaItemProxyTypeRegistered = true;
    }

    private static string ResolveDisplayName(MobileTemplateDefinition template)
        => !string.IsNullOrWhiteSpace(template.Name)
               ? template.Name.Trim()
               : !string.IsNullOrWhiteSpace(template.Title)
                   ? template.Title.Trim()
                   : template.Id;

    private static IReadOnlyList<LuaMobileTemplateSearchResult> SearchTemplateDefinitions(
        IReadOnlyList<MobileTemplateDefinition> templates,
        string query
    )
    {
        var orderedTemplates = templates.OrderBy(static template => template.Id, StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(query))
        {
            return orderedTemplates.Select(MapSearchResult).ToList();
        }

        var prefixMatches = new List<LuaMobileTemplateSearchResult>();
        var substringMatches = new List<LuaMobileTemplateSearchResult>();

        foreach (var template in orderedTemplates)
        {
            var displayName = ResolveDisplayName(template);
            var matchesPrefix = template.Id.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
                                displayName.StartsWith(query, StringComparison.OrdinalIgnoreCase);

            if (matchesPrefix)
            {
                prefixMatches.Add(MapSearchResult(template));

                continue;
            }

            if (template.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                displayName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                substringMatches.Add(MapSearchResult(template));
            }
        }

        return [..prefixMatches, ..substringMatches];
    }

    private static LuaMobileTemplateSearchResult MapSearchResult(MobileTemplateDefinition template)
        => new(template.Id, ResolveDisplayName(template));

    private UOMobileEntity? ResolveRiderForMount(Serial riderId)
    {
        if (_gameNetworkSessionService.TryGetByCharacterId(riderId, out var session) && session.Character is not null)
        {
            return session.Character;
        }

        return TryResolveRuntimeMobile(riderId) ??
               _mobileService?.GetAsync(riderId).GetAwaiter().GetResult();
    }

    private LuaItemProxy? CreateLuaItemProxy(UOItemEntity? item)
    {
        if (item is null)
        {
            return null;
        }

        RegisterLuaTypeIfNeeded();

        return new(item, _itemService, _spatialWorldService, _speechService);
    }

    private LuaMobileProxy? CreateLuaMobileProxy(UOMobileEntity? mobile)
    {
        if (mobile is null)
        {
            return null;
        }

        RegisterLuaTypeIfNeeded();

        return new(
            mobile,
            _speechService,
            _gameNetworkSessionService,
            _spatialWorldService,
            _movementValidationService,
            _pathfindingService,
            _gameEventBusService,
            _backgroundJobService
        );
    }

    private static int CountMatchingItems(IEnumerable<UOItemEntity> containers, int itemId)
        => containers.Sum(container => CountMatchingItems(container, itemId));

    private static int CountMatchingItems(UOItemEntity container, int itemId)
    {
        var count = 0;

        foreach (var child in container.Items)
        {
            if (child.ItemId == itemId)
            {
                count += Math.Max(0, child.Amount);
            }

            if (child.Items.Count > 0)
            {
                count += CountMatchingItems(child, itemId);
            }
        }

        return count;
    }

    private IEnumerable<UOItemEntity> GetConsumableSources(UOMobileEntity mobile)
    {
        var quiver = mobile.GetEquippedItemsRuntime().FirstOrDefault(static item => item.IsQuiver);

        if (quiver is not null)
        {
            yield return quiver;
        }

        var backpack = TryResolveBackpack(mobile);

        if (backpack is not null)
        {
            yield return backpack;
        }
    }

    private static Serial ResolveBackpackId(UOMobileEntity mobile)
    {
        if (mobile.BackpackId != Serial.Zero)
        {
            return mobile.BackpackId;
        }

        return mobile.EquippedItemIds.TryGetValue(ItemLayerType.Backpack, out var equippedBackpackId)
                   ? equippedBackpackId
                   : Serial.Zero;
    }

    private UOMobileEntity? ResolveMobile(Serial mobileId)
    {
        if (mobileId == Serial.Zero)
        {
            return null;
        }

        if (_gameNetworkSessionService.TryGetByCharacterId(mobileId, out var session) && session.Character is not null)
        {
            return session.Character;
        }

        return TryResolveRuntimeMobile(mobileId) ??
               _mobileService?.GetAsync(mobileId).GetAwaiter().GetResult() ??
               _characterService.GetCharacterAsync(mobileId).GetAwaiter().GetResult();
    }

    private static UOItemEntity? TryResolveBackpack(UOMobileEntity mobile)
    {
        var backpackId = ResolveBackpackId(mobile);

        return mobile.GetEquippedItemsRuntime()
                     .FirstOrDefault(item => item.Id == backpackId || item.EquippedLayer == ItemLayerType.Backpack);
    }

    private static UOItemEntity? TryResolveWeapon(UOMobileEntity mobile)
        => mobile.GetEquippedItemsRuntime()
                 .FirstOrDefault(
                     item => item.EquippedLayer is ItemLayerType.OneHanded or ItemLayerType.TwoHanded &&
                             (item.WeaponSkill is not null || item.CombatStats is not null)
                 );

    private bool TryConsumeFromSources(IEnumerable<UOItemEntity> sources, int itemId)
    {
        foreach (var source in sources)
        {
            if (!TryConsumeItemRecursive(source, itemId, out var changedStack, out var deletedStack))
            {
                continue;
            }

            if (changedStack is not null)
            {
                _itemService!.UpsertItemAsync(changedStack).GetAwaiter().GetResult();
            }

            if (deletedStack is not null)
            {
                _ = _itemService!.DeleteItemAsync(deletedStack.Id).GetAwaiter().GetResult();
            }

            _itemService!.UpsertItemAsync(source).GetAwaiter().GetResult();

            return true;
        }

        return false;
    }

    private static bool TryConsumeItemRecursive(
        UOItemEntity container,
        int itemId,
        out UOItemEntity? changedStack,
        out UOItemEntity? deletedStack
    )
    {
        changedStack = null;
        deletedStack = null;

        for (var index = container.Items.Count - 1; index >= 0; index--)
        {
            var child = container.Items[index];

            if (child.ItemId == itemId)
            {
                child.Amount--;

                if (child.Amount <= 0)
                {
                    container.RemoveItem(child.Id);
                    deletedStack = child;
                }
                else
                {
                    changedStack = child;
                }

                return true;
            }

            if (TryConsumeItemRecursive(child, itemId, out changedStack, out deletedStack))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveSkillName(string skillName, out UOSkillName resolvedSkill)
    {
        resolvedSkill = default;

        if (string.IsNullOrWhiteSpace(skillName))
        {
            return false;
        }

        var normalized = new string(skillName.Where(char.IsLetterOrDigit).ToArray());

        foreach (var candidate in Enum.GetValues<UOSkillName>())
        {
            var candidateName = new string(candidate.ToString().Where(char.IsLetterOrDigit).ToArray());

            if (string.Equals(candidateName, normalized, StringComparison.OrdinalIgnoreCase))
            {
                resolvedSkill = candidate;

                return true;
            }
        }

        return false;
    }

    private static bool TryGetRequiredInt(Table table, string key, out int value)
    {
        value = 0;
        var dyn = table.Get(key);

        switch (dyn.Type)
        {
            case DataType.Number:
                value = (int)dyn.Number;

                return true;
            case DataType.String when int.TryParse(dyn.String, out var parsed):
                value = parsed;

                return true;
            default:
                return false;
        }
    }

    private static bool TryParsePosition(Table? position, out Point3D location, out int mapId)
    {
        location = Point3D.Zero;
        mapId = 0;

        if (position is null)
        {
            return false;
        }

        if (!TryGetRequiredInt(position, "x", out var x) ||
            !TryGetRequiredInt(position, "y", out var y) ||
            !TryGetRequiredInt(position, "z", out var z) ||
            !TryGetRequiredInt(position, "map_id", out mapId))
        {
            return false;
        }

        location = new(x, y, z);

        return true;
    }

    private UOMobileEntity? TryResolveRuntimeMobile(Serial mobileId)
    {
        if (_spatialWorldService is null)
        {
            return null;
        }

        foreach (var sector in _spatialWorldService.GetActiveSectors())
        {
            var runtimeMobile = sector.GetEntity<UOMobileEntity>(mobileId);

            if (runtimeMobile is not null)
            {
                return runtimeMobile;
            }
        }

        return null;
    }
}
