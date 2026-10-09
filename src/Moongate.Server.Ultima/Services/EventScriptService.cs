using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Loads
///     <c>
///         scripts/events/*.lua
///     </c>
///     and calls their functions, as the gump scripts are called: each file defines a global
///     table with the name of the file.
/// </summary>
public sealed class EventScriptService : IEventScriptService, IMoongateStartupService
{
    public const string EventsDirectory = "events";

    private readonly ILogger _logger = Log.ForContext<EventScriptService>();
    private readonly IScriptEngine _engine;
    private readonly IGameLoopService _loop;
    private readonly ScriptEngineOptions _options;

    private bool _running;

    public EventScriptService(IScriptEngine engine, IGameLoopService loop, ScriptEngineOptions options)
    {
        _engine = engine;
        _loop = loop;
        _options = options;
    }

    public async Task StartAsync()
    {
        await ScriptDirectoryLoader.LoadAsync(_engine, _loop, _options.ScriptsDirectory, EventsDirectory, _logger);
        _running = true;
    }

    public Task StopAsync()
    {
        _running = false;

        return Task.CompletedTask;
    }

    public ScriptResult Call(string script, string function, params object?[] args)
    {
        return _running
            ? _engine.CallMember($"{EventsDirectory}/{script}.lua", script, function, args)
            : ScriptResult.Missing;
    }
}
