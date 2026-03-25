using System.Globalization;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Descriptors;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Data.Internal.Templates;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Templates.Items;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("item", "Provides helpers to resolve items from scripts.")]

/// <summary>
/// Exposes item lookup helpers to Lua scripts.
/// </summary>
public sealed class ItemModule
{
    private const int MaxTemplateSearchPageSize = 50;
    private static bool _isLuaItemProxyTypeRegistered;
    private readonly IItemService _itemService;
    private readonly ISpatialWorldService? _spatialWorldService;
    private readonly ISpeechService? _speechService;
    private readonly IItemTemplateService? _itemTemplateService;

    public ItemModule(
        IItemService itemService,
        ISpatialWorldService? spatialWorldService = null,
        ISpeechService? speechService = null,
        IItemTemplateService? itemTemplateService = null
    )
    {
        _itemService = itemService;
        _spatialWorldService = spatialWorldService;
        _speechService = speechService;
        _itemTemplateService = itemTemplateService;
    }

    [ScriptFunction("get", "Gets an item reference by item id, or nil when not found.")]
    public LuaItemProxy? Get(uint itemId)
    {
        if (itemId == 0)
        {
            return null;
        }

        RegisterLuaTypeIfNeeded();
        var item = _itemService.GetItemAsync((Serial)itemId).GetAwaiter().GetResult();

        return item is null ? null : new(item, _itemService, _spatialWorldService, _speechService);
    }

    [ScriptFunction("spawn", "Spawns an item template at world position { x, y, z, map_id }.")]
    public LuaItemProxy? Spawn(string itemTemplateId, Table? position, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemTemplateId) ||
            amount <= 0 ||
            !TryParsePosition(position, out var location, out var mapId))
        {
            return null;
        }

        RegisterLuaTypeIfNeeded();
        var item = _itemService.SpawnFromTemplateAsync(itemTemplateId.Trim()).GetAwaiter().GetResult();

        if (amount > 1)
        {
            item.Amount = amount;
            _itemService.UpsertItemAsync(item).GetAwaiter().GetResult();
        }

        var moved = _itemService.MoveItemToWorldAsync(item.Id, location, mapId).GetAwaiter().GetResult();

        if (!moved)
        {
            return null;
        }

        var resolved = _itemService.GetItemAsync(item.Id).GetAwaiter().GetResult() ?? item;
        _spatialWorldService?.AddOrUpdateItem(resolved, mapId);

        return new(resolved, _itemService, _spatialWorldService, _speechService);
    }

    [ScriptFunction("search_templates", "Searches item templates for GM tools and returns paged results.")]
    public Table SearchTemplates(string query, int page = 1, int pageSize = 20)
    {
        var results = new Table(null);

        if (_itemTemplateService is null)
        {
            return results;
        }

        var normalizedQuery = query?.Trim() ?? string.Empty;
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxTemplateSearchPageSize);
        var matches = SearchTemplateDefinitions(_itemTemplateService.GetAll(), normalizedQuery);
        var paged = matches.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize);
        var index = 1;

        foreach (var match in paged)
        {
            var entry = new Table(null)
            {
                ["template_id"] = match.TemplateId,
                ["display_name"] = match.DisplayName,
                ["item_id"] = match.ItemId
            };
            results[index++] = entry;
        }

        return results;
    }

    private static void RegisterLuaTypeIfNeeded()
    {
        if (_isLuaItemProxyTypeRegistered)
        {
            return;
        }

        var type = typeof(LuaItemProxy);
        UserData.RegisterType(type, new GenericUserDataDescriptor(type));
        _isLuaItemProxyTypeRegistered = true;
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

    private static IReadOnlyList<LuaItemTemplateSearchResult> SearchTemplateDefinitions(
        IReadOnlyList<ItemTemplateDefinition> templates,
        string query
    )
    {
        var orderedTemplates = templates.OrderBy(static template => template.Id, StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(query))
        {
            return orderedTemplates.Select(MapSearchResult).ToList();
        }

        var prefixMatches = new List<LuaItemTemplateSearchResult>();
        var substringMatches = new List<LuaItemTemplateSearchResult>();

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

    private static LuaItemTemplateSearchResult MapSearchResult(ItemTemplateDefinition template)
        => new(template.Id, ResolveDisplayName(template), ParseItemId(template.ItemId));

    private static int ParseItemId(string itemIdText)
    {
        if (string.IsNullOrWhiteSpace(itemIdText))
        {
            return 0;
        }

        var value = itemIdText.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(
                value.AsSpan(2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var itemId
            )
                       ? itemId
                       : 0;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedItemId)
                   ? parsedItemId
                   : 0;
    }

    private static string ResolveDisplayName(ItemTemplateDefinition template)
        => string.IsNullOrWhiteSpace(template.Name) ? template.Id : template.Name.Trim();
}
