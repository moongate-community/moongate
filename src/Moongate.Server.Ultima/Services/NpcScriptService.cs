using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Runs the mobile scripts: loads every <c>scripts/mobiles/*.lua</c> at startup and calls the functions of the global
///     table an NPC's template names with <c>script_id</c>, defined by <c>scripts/mobiles/&lt;script_id&gt;.lua</c>. It
///     is the NPC thinker: each think calls <c>on_think(serial)</c>, which is instantaneous, as ModernUO's; a script
///     that waits in it is warned once. It calls nothing before the scripts are loaded, which is after the NPCs are,
///     nor once stopped, before the script engine.
/// </summary>
public sealed class NpcScriptService : INpcScriptService, INpcThinker, IMoongateStartupService
{
    public const string MobilesDirectory = "mobiles";

    private readonly ILogger _logger;
    private readonly IScriptEngine _engine;
    private readonly IMobileTemplateService _templates;
    private readonly IGameLoopService _loop;
    private readonly ScriptEngineOptions _options;
    private readonly HashSet<string> _warnedWait = new(StringComparer.Ordinal);

    private bool _running;

    public NpcScriptService(
        IScriptEngine engine,
        IMobileTemplateService templates,
        IGameLoopService loop,
        ScriptEngineOptions options,
        ILogger? logger = null
    )
    {
        _engine = engine;
        _templates = templates;
        _loop = loop;
        _options = options;
        _logger = (logger ?? Log.Logger).ForContext<NpcScriptService>();
    }

    public async Task StartAsync()
    {
        await ScriptDirectoryLoader.LoadAsync(_engine, _loop, _options.ScriptsDirectory, MobilesDirectory, _logger);
        _running = true;
    }

    public Task StopAsync()
    {
        _running = false;

        return Task.CompletedTask;
    }

    public void Think(MobileEntity npc)
    {
        var result = Run(npc, "on_think");

        if (result.Kind == ScriptResultKind.Suspended && ScriptOf(npc) is { } script && _warnedWait.Add(script))
        {
            _logger.Warning(
                "Mobile script {Script}: on_think called wait(); a think must not wait, keep the timing in the script",
                script
            );
        }
    }

    /// <inheritdoc />
    public ScriptResult Run(MobileEntity npc, string function, params object?[] args)
    {
        if (!_running || ScriptOf(npc) is not { } script)
        {
            return ScriptResult.Missing;
        }

        return _engine.CallMember(
            $"{MobilesDirectory}/{script}.lua",
            script,
            function,
            [(long)npc.Id.Value, ..args]
        );
    }

    /// <inheritdoc />
    public void Queue(MobileEntity npc, string function, params object?[] args)
    {
        if (!_running || ScriptOf(npc) is null)
        {
            return;
        }

        if (!_loop.TryPost(new LoopActionWorkItem(() => Run(npc, function, args))))
        {
            _logger.Warning("NPC {Serial}: {Function} was dropped, the game loop is full or stopping", npc.Id, function);
        }
    }

    private string? ScriptOf(MobileEntity npc)
    {
        return npc.TemplateId is { } id && _templates.TryGet(id, out var template) ? template.ScriptId : null;
    }
}
