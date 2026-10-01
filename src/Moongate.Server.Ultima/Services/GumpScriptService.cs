using Lua;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Loads <c>scripts/gumps/*.lua</c> at startup and calls the gump scripts' functions.
/// </summary>
public sealed class GumpScriptService : IGumpScriptService, IMoongateStartupService
{
    public const string GumpsDirectory = "gumps";

    private readonly ILogger _logger = Log.ForContext<GumpScriptService>();
    private readonly IScriptEngine _engine;
    private readonly IGameLoopService _loop;
    private readonly ScriptEngineOptions _options;

    private bool _running;

    public GumpScriptService(IScriptEngine engine, IGameLoopService loop, ScriptEngineOptions options)
    {
        _engine = engine;
        _loop = loop;
        _options = options;
    }

    public async Task StartAsync()
    {
        await ScriptDirectoryLoader.LoadAsync(_engine, _loop, _options.ScriptsDirectory, GumpsDirectory, _logger);
        _running = true;
    }

    public Task StopAsync()
    {
        _running = false;

        return Task.CompletedTask;
    }

    public ScriptResult CallFunction(string gumpId, LuaFunction function, params object?[] args)
    {
        return _running
            ? _engine.CallFunction($"{GumpsDirectory}/{gumpId}.lua", function, args)
            : ScriptResult.Missing;
    }

    public ScriptResult Call(string gumpId, string function, params object?[] args)
    {
        return _running
            ? _engine.CallMember($"{GumpsDirectory}/{gumpId}.lua", gumpId, function, args)
            : ScriptResult.Missing;
    }
}
