using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Descriptors;
using Moongate.Server.Data.Interaction;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Data.Internal.Scripting;
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
    private readonly MobileCombatModule _combatModule;
    private readonly MobileInventoryModule _inventoryModule;
    private readonly MobileMovementModule _movementModule;

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
        _movementModule = new(
            speechService,
            gameNetworkSessionService,
            spatialWorldService,
            movementValidationService,
            pathfindingService,
            gameEventBusService,
            backgroundJobService
        );
        _combatModule = new(
            mobileService,
            gameNetworkSessionService,
            spatialWorldService,
            outgoingPacketQueue,
            skillGainService,
            skillCheckRollProvider
        );
        _inventoryModule = new(itemService);
    }

    [ScriptFunction("dismount", "Attempts to dismount the rider from the current mount.")]
    public bool Dismount(uint riderId)
    {
        if (riderId == 0 || _mobileService is null)
        {
            return false;
        }

        var dismounted = _combatModule.Dismount((Serial)riderId);

        if (dismounted)
        {
            _combatModule.RefreshMountedSession((Serial)riderId, Serial.Zero, false);
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

    [ScriptFunction("get_brain_id", "Gets the resolved runtime brain id for a character id, or nil when unavailable.")]
    public string? GetBrainId(uint characterId)
    {
        var mobile = ResolveMobile((Serial)characterId);

        return mobile?.BrainId;
    }

    [ScriptFunction("get_ai_fight_mode", "Gets the resolved runtime AI fight mode for a character id, or nil when unavailable.")]
    public string? GetAiFightMode(uint characterId)
    {
        var mobile = ResolveMobile((Serial)characterId);

        return TryGetCustomString(mobile, MobileCustomParamKeys.Ai.FightMode);
    }

    [ScriptFunction(
        "get_ai_range_perception",
        "Gets the resolved runtime AI perception range for a character id, or nil when unavailable."
    )]
    public int? GetAiRangePerception(uint characterId)
    {
        var mobile = ResolveMobile((Serial)characterId);

        return TryGetCustomInteger(mobile, MobileCustomParamKeys.Ai.RangePerception);
    }

    [ScriptFunction("get_ai_range_fight", "Gets the resolved runtime AI fight range for a character id, or nil when unavailable.")]
    public int? GetAiRangeFight(uint characterId)
    {
        var mobile = ResolveMobile((Serial)characterId);

        return TryGetCustomInteger(mobile, MobileCustomParamKeys.Ai.RangeFight);
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

        return _movementModule.Teleport(mobile, mapId, x, y, z);
    }

    [ScriptFunction("check_skill", "Checks a skill against a min/max range and applies gain rules.")]
    public bool CheckSkill(uint characterId, string skillName, double minSkill, double maxSkill, uint targetId = 0)
    {
        var mobile = ResolveMobile((Serial)characterId);

        if (mobile is null)
        {
            return false;
        }

        return _combatModule.CheckSkill(mobile, skillName, minSkill, maxSkill, targetId);
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

        return _inventoryModule.ConsumeItem(mobile, itemId, amount);
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

        return CreateLuaItemProxy(_inventoryModule.AddItemToBackpack(mobile, itemTemplateId, amount));
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
        _spatialWorldService?.AddOrUpdateMobile(mobile);

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

        var mounted = _combatModule.TryMount(riderSerial, mountSerial, rider, mount);

        if (mounted)
        {
            _combatModule.RefreshMountedSession(riderSerial, mountSerial, true);
        }

        return mounted;
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

    private static int? TryGetCustomInteger(UOMobileEntity? mobile, string key)
    {
        if (mobile is null || !mobile.TryGetCustomInteger(key, out var value))
        {
            return null;
        }

        if (value is < int.MinValue or > int.MaxValue)
        {
            return null;
        }

        return (int)value;
    }

    private static string? TryGetCustomString(UOMobileEntity? mobile, string key)
    {
        if (mobile is null || !mobile.TryGetCustomString(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value;
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
