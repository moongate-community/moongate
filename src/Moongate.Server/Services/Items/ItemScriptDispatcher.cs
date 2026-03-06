using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Items;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using MoonSharp.Interpreter;
using Serilog;

namespace Moongate.Server.Services.Items;

[RegisterGameEventListener]

/// <summary>
/// Resolves and invokes Lua callbacks for item script hooks.
/// </summary>
public sealed partial class ItemScriptDispatcher
    : IItemScriptDispatcher, IGameEventListener<ItemSingleClickEvent>, IGameEventListener<ItemDoubleClickEvent>
{
    private readonly ILogger _logger = Log.ForContext<ItemScriptDispatcher>();
    private readonly IItemService _itemService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly Script? _luaScript;
    private readonly bool _supportsMoonSharpRuntime;
    private readonly ConcurrentDictionary<string, bool> _hookAvailabilityCache = new(StringComparer.Ordinal);

    public ItemScriptDispatcher(
        IScriptEngineService scriptEngineService,
        IItemService itemService,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _itemService = itemService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _luaScript = (scriptEngineService as LuaScriptEngineService)?.LuaScript;
        _supportsMoonSharpRuntime = _luaScript is not null;
    }

    public Task<bool> DispatchAsync(ItemScriptContext context, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.Hook))
        {
            return Task.FromResult(false);
        }

        if (!_supportsMoonSharpRuntime || _luaScript is null)
        {
            _logger.Warning("Item script dispatch requires MoonSharp runtime. ScriptId={ScriptId}", context.Item.ScriptId);

            return Task.FromResult(false);
        }

        try
        {
            if (!TryResolveHookFunction(
                    ResolveTableCandidates(context.Item),
                    ResolveHookCandidates(context.Hook),
                    out var hookFunction
                ))
            {
                return Task.FromResult(false);
            }

            var payload = BuildLuaContextPayload(context);
            _luaScript.Call(hookFunction!, payload);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "Failed to dispatch item script hook '{Hook}' for script '{ScriptId}'",
                context.Hook,
                context.Item.ScriptId
            );

            return Task.FromResult(false);
        }
    }

    public async Task HandleAsync(ItemSingleClickEvent gameEvent, CancellationToken cancellationToken = default)
        => await HandleItemEvent(true, gameEvent.ItemSerial, gameEvent.SessionId);

    public async Task HandleAsync(ItemDoubleClickEvent gameEvent, CancellationToken cancellationToken = default)
        => await HandleItemEvent(false, gameEvent.ItemSerial, gameEvent.SessionId);

    public bool HasHook(UOItemEntity item, string hook)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!_supportsMoonSharpRuntime || _luaScript is null || string.IsNullOrWhiteSpace(hook))
        {
            return false;
        }

        var normalizedHook = NormalizeToken(hook);
        var scriptIdentity = ResolveScriptIdentity(item);
        var cacheKey = $"{scriptIdentity}|{normalizedHook}";

        if (_hookAvailabilityCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var hasHook = TryResolveHookFunction(
            ResolveTableCandidates(item),
            ResolveHookCandidates(hook),
            out _
        );

        _hookAvailabilityCache[cacheKey] = hasHook;

        return hasHook;
    }

    private static Dictionary<string, object?> BuildLuaContextPayload(ItemScriptContext context)
        => new()
        {
            ["hook"] = context.Hook,
            ["session_id"] = context.Session?.SessionId,
            ["mobile_id"] = context.Mobile is null ? null : (uint)context.Mobile.Id,
            ["metadata"] = context.Metadata,
            ["item"] = new Dictionary<string, object?>
            {
                ["serial"] = (uint)context.Item.Id,
                ["script_id"] = context.Item.ScriptId,
                ["name"] = context.Item.Name,
                ["map_id"] = context.Item.MapId,
                ["item_id"] = context.Item.ItemId,
                ["amount"] = context.Item.Amount,
                ["hue"] = context.Item.Hue,
                ["location"] = new Dictionary<string, int>
                {
                    ["x"] = context.Item.Location.X,
                    ["y"] = context.Item.Location.Y,
                    ["z"] = context.Item.Location.Z
                }
            }
        };

    private async Task HandleItemEvent(bool isSingleClick, Serial itemId, long sessionId)
    {
        var (Found, Item) = await _itemService.TryToGetItemAsync(itemId);

        if (Found)
        {
            if (_gameNetworkSessionService.TryGet(sessionId, out var session))
            {
                await DispatchAsync(new(session, Item, isSingleClick ? "single_click" : "double_click"));
            }
        }
    }

    [GeneratedRegex("[^a-zA-Z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonAlphaNumericRegex();

    private static string NormalizeToken(string token)
    {
        var normalized = NonAlphaNumericRegex()
                         .Replace(token, "_")
                         .Trim('_')
                         .ToLowerInvariant();

        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }

    private static IEnumerable<string> ResolveHookCandidates(string hook)
    {
        var normalizedHook = NormalizeToken(hook);

        if (normalizedHook == "single_click")
        {
            return ["on_click", "OnClick", "on_single_click", "OnSingleClick"];
        }

        if (normalizedHook == "double_click")
        {
            return ["on_double_click", "OnDoubleClick"];
        }

        var pascal = ToPascalCase(normalizedHook);

        if (normalizedHook.StartsWith("on_", StringComparison.Ordinal))
        {
            return [normalizedHook, pascal];
        }

        return [$"on_{normalizedHook}", $"On{pascal}", normalizedHook];
    }

    private static string ResolveScriptIdentity(UOItemEntity item)
    {
        var scriptId = item.ScriptId?.Trim();

        if (!string.IsNullOrWhiteSpace(scriptId) &&
            !string.Equals(scriptId, "none", StringComparison.OrdinalIgnoreCase))
        {
            return $"sid:{NormalizeToken(scriptId)}";
        }

        var normalizedName = NormalizeToken(item.Name ?? string.Empty);

        return $"name:{normalizedName}";
    }

    private DynValue? ResolveScriptTable(string tableName)
    {
        if (_luaScript is null)
        {
            return null;
        }

        return _luaScript.Globals.Get(tableName) is { Type: DataType.Table } table ? table : null;
    }

    private static IEnumerable<string> ResolveTableCandidates(UOItemEntity item)
    {
        var scriptId = item.ScriptId?.Trim();

        if (!string.IsNullOrWhiteSpace(scriptId) &&
            !string.Equals(scriptId, "none", StringComparison.OrdinalIgnoreCase))
        {
            yield return NormalizeToken(scriptId);

            yield break;
        }

        var normalizedName = NormalizeToken(item.Name ?? string.Empty);

        if (!string.Equals(normalizedName, "unknown", StringComparison.Ordinal))
        {
            yield return normalizedName;
            yield return $"items_{normalizedName}";
        }
    }

    private static DynValue? ResolveTableFunction(DynValue table, string functionName)
    {
        if (table.Type != DataType.Table || table.Table is null)
        {
            return null;
        }

        var function = table.Table.Get(functionName);

        return function.Type == DataType.Function ? function : null;
    }

    private static string ToPascalCase(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return "Unknown";
        }

        var parts = token.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return "Unknown";
        }

        return string.Concat(
            parts.Select(
                static part => part.Length == 1
                                   ? part.ToUpperInvariant()
                                   : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()
            )
        );
    }

    private bool TryResolveHookFunction(
        IEnumerable<string> tableCandidates,
        IEnumerable<string> hookCandidates,
        out DynValue? hookFunction
    )
    {
        hookFunction = null;

        foreach (var tableName in tableCandidates)
        {
            var scriptTable = ResolveScriptTable(tableName);

            if (scriptTable is null)
            {
                continue;
            }

            foreach (var hookName in hookCandidates)
            {
                var resolvedFunction = ResolveTableFunction(scriptTable, hookName);

                if (resolvedFunction is null)
                {
                    continue;
                }

                hookFunction = resolvedFunction;

                return true;
            }
        }

        return false;
    }
}
