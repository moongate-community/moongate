using System.Text.RegularExpressions;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Items;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Ids;
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
    private readonly IScriptEngineService _scriptEngineService;
    private readonly IItemService _itemService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public ItemScriptDispatcher(
        IScriptEngineService scriptEngineService,
        IItemService itemService,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _scriptEngineService = scriptEngineService;
        _itemService = itemService;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    public Task<bool> DispatchAsync(ItemScriptContext context, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.Item.ScriptId) || string.IsNullOrWhiteSpace(context.Hook))
        {
            return Task.FromResult(false);
        }

        var tableName = NormalizeToken(context.Item.ScriptId);
        var contextGlobalName = "__item_script_dispatch_context";
        _scriptEngineService.RegisterGlobal(contextGlobalName, BuildLuaContextPayload(context));

        try
        {
            foreach (var hook in ResolveHookCandidates(context.Hook))
            {
                var command = BuildTableDispatchCommand(tableName, hook, contextGlobalName);
                var result = _scriptEngineService.ExecuteFunction(command);

                if (result.Success && result.Data is bool dispatched && dispatched)
                {
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "Failed to dispatch item script hook '{Hook}' for script '{ScriptId}' table '{TableName}'",
                context.Hook,
                context.Item.ScriptId,
                tableName
            );

            return Task.FromResult(false);
        }
        finally
        {
            _scriptEngineService.UnregisterGlobal(contextGlobalName);
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

    private static string BuildTableDispatchCommand(string tableName, string hook, string contextGlobalName)
    {
        return $"""
                (function()
                    local t = {tableName}
                    if type(t) ~= "table" then
                        return false
                    end
                    local f = t["{hook}"]
                    if type(f) ~= "function" then
                        return false
                    end
                    f({contextGlobalName})
                    return true
                end)()
                """;
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
