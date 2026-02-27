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

        var functionName = BuildFunctionName(context.Item.ScriptId, context.Hook);

        try
        {
            _scriptEngineService.CallFunction(functionName, context);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "Failed to dispatch item script hook '{Hook}' for script '{ScriptId}' using function '{FunctionName}'",
                context.Hook,
                context.Item.ScriptId,
                functionName
            );

            return Task.FromResult(false);
        }
    }

    private static string BuildFunctionName(string scriptId, string hook)
    {
        var normalizedScriptId = NormalizeToken(scriptId);
        var normalizedHook = NormalizeToken(hook);

        return $"on_item_{normalizedScriptId}_{normalizedHook}";
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
