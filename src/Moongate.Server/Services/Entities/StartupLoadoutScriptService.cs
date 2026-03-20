using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Entities;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Data.Startup;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;
using Serilog;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Resolves the initial starter loadout through a Lua hook.
/// </summary>
public sealed class StartupLoadoutScriptService : IStartupLoadoutScriptService
{
    private const string BuildStartingLoadoutFunctionName = "build_starting_loadout";

    private readonly ILogger _logger = Log.ForContext<StartupLoadoutScriptService>();
    private readonly Script _luaScript;

    public StartupLoadoutScriptService(IScriptEngineService scriptEngineService)
    {
        ArgumentNullException.ThrowIfNull(scriptEngineService);

        _luaScript = (scriptEngineService as LuaScriptEngineService)?.LuaScript
                     ?? throw new ArgumentException(
                         "StartupLoadoutScriptService requires LuaScriptEngineService.",
                         nameof(scriptEngineService)
                     );
    }

    /// <inheritdoc />
    public StartupLoadout BuildLoadout(StarterProfileContext profileContext, string playerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

        var hook = _luaScript.Globals.Get(BuildStartingLoadoutFunctionName);

        if (hook.Type != DataType.Function)
        {
            _logger.Debug("Lua startup loadout hook {HookName} is not defined", BuildStartingLoadoutFunctionName);

            return new();
        }

        var context = CreateContext(profileContext, playerName);
        var contextTable = new Table(_luaScript)
        {
            ["player_name"] = context.PlayerName,
            ["race"] = context.Race,
            ["gender"] = context.Gender,
            ["profession"] = context.Profession
        };

        var result = _luaScript.Call(hook, DynValue.NewTable(contextTable));

        if (result.Type is DataType.Nil or DataType.Void)
        {
            return new();
        }

        if (result.Type != DataType.Table || result.Table is null)
        {
            throw new InvalidOperationException(
                $"Lua startup loadout hook '{BuildStartingLoadoutFunctionName}' must return a table."
            );
        }

        return StartupLoadoutScriptResultParser.Parse(result.Table);
    }

    private static StartupLoadoutScriptContext CreateContext(StarterProfileContext profileContext, string playerName)
        => new()
        {
            PlayerName = playerName.Trim(),
            Race = profileContext.Race?.Name?.Trim().ToLowerInvariant() ?? "human",
            Gender = profileContext.Gender == GenderType.Female ? "female" : "male",
            Profession = profileContext.Profession.Name?.Trim() ?? string.Empty
        };
}
