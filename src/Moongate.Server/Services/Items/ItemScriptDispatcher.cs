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
            var payload = BuildLuaContextPayload(context);

            foreach (var tableName in ResolveTableCandidates(context))
            {
                var scriptTable = ResolveScriptTable(tableName);
                if (scriptTable is null)
                {
                    continue;
                }

                foreach (var hook in ResolveHookCandidates(context.Hook))
                {
                    var hookFunction = ResolveTableFunction(scriptTable, hook);

                    if (hookFunction is null)
                    {
                        continue;
                    }

                    _luaScript.Call(hookFunction, payload);

                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
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

    private static IEnumerable<string> ResolveTableCandidates(ItemScriptContext context)
    {
        var scriptId = context.Item.ScriptId?.Trim();

        if (!string.IsNullOrWhiteSpace(scriptId) &&
            !string.Equals(scriptId, "none", StringComparison.OrdinalIgnoreCase))
        {
            yield return NormalizeToken(scriptId);
            yield break;
        }

        var normalizedName = NormalizeToken(context.Item.Name ?? string.Empty);

        if (!string.Equals(normalizedName, "unknown", StringComparison.Ordinal))
        {
            yield return normalizedName;
            yield return $"items_{normalizedName}";
        }
    }

    private DynValue? ResolveScriptTable(string tableName)
    {
        if (_luaScript is null)
        {
            return null;
        }

        return _luaScript.Globals.Get(tableName) is { Type: DataType.Table } table ? table : null;
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

    private static Dictionary<string, object?> BuildLuaContextPayload(ItemScriptContext context)
    {
        return new()
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
    }

    private static string NormalizeToken(string token)
    {
        var normalized = NonAlphaNumericRegex()
                         .Replace(token, "_")
                         .Trim('_')
                         .ToLowerInvariant();

        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }

    [GeneratedRegex("[^a-zA-Z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonAlphaNumericRegex();

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

        return string.Concat(parts.Select(
            static part => part.Length == 1
                               ? part.ToUpperInvariant()
                               : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()
        ));
    }

    public async Task HandleAsync(ItemSingleClickEvent gameEvent, CancellationToken cancellationToken = default)
    {
        await HandleItemEvent(true, gameEvent.ItemSerial, gameEvent.SessionId);
    }

    public async Task HandleAsync(ItemDoubleClickEvent gameEvent, CancellationToken cancellationToken = default)
    {
        await HandleItemEvent(false, gameEvent.ItemSerial, gameEvent.SessionId);
    }

    private async Task HandleItemEvent(bool isSingleClick, Serial itemId, long sessionId)
    {
        var (Found, Item) = await _itemService.TryToGetItemAsync(itemId);

        if (Found)
        {
            if (_gameNetworkSessionService.TryGet(sessionId, out var session))
            {
                await DispatchAsync(
                    new ItemScriptContext(session, Item, isSingleClick ? "single_click" : "double_click")
                );
            }
        }
    }
}
