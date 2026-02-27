using System.Text.RegularExpressions;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Data.Items;
using Moongate.Server.Interfaces.Items;
using Serilog;

namespace Moongate.Server.Services.Items;

/// <summary>
/// Resolves and invokes Lua callbacks for item script hooks.
/// </summary>
public sealed partial class ItemScriptDispatcher : IItemScriptDispatcher
{
    private readonly ILogger _logger = Log.ForContext<ItemScriptDispatcher>();
    private readonly IScriptEngineService _scriptEngineService;

    public ItemScriptDispatcher(IScriptEngineService scriptEngineService)
        => _scriptEngineService = scriptEngineService;

    public Task<bool> DispatchAsync(ItemScriptContext context, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.Item.ScriptId))
        {
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(context.Hook))
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
        var normalized = NonAlphaNumericRegex().Replace(token, "_")
            .Trim('_')
            .ToLowerInvariant();

        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }

    [GeneratedRegex("[^a-zA-Z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonAlphaNumericRegex();
}
