using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Runs the mobile scripts: loads every <c>scripts/mobiles/*.lua</c> at startup and, on each think of an NPC, calls
///     <c>on_think(serial)</c> of the global table its template names with <c>script_id</c>. A think is instantaneous,
///     as ModernUO's: a script that waits in it is warned once.
/// </summary>
public sealed class NpcScriptService : INpcThinker, IMoongateStartupService
{
    public const string MobilesDirectory = "mobiles";

    private readonly ILogger _logger;
    private readonly IScriptEngine _engine;
    private readonly IMobileTemplateService _templates;
    private readonly IGameLoopService _loop;
    private readonly ScriptEngineOptions _options;
    private readonly HashSet<string> _warnedWait = new(StringComparer.Ordinal);

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
        var directory = Path.Combine(_options.ScriptsDirectory, MobilesDirectory);

        if (!Directory.Exists(directory))
        {
            _logger.Debug("No mobile scripts: {Directory} does not exist", directory);

            return;
        }

        var files = Directory.GetFiles(directory, "*.lua", SearchOption.TopDirectoryOnly)
                             .Select(Path.GetFileName)
                             .Order(StringComparer.Ordinal)
                             .ToList();
        var work = new LoopActionWorkItem(() =>
            {
                foreach (var file in files)
                {
                    try
                    {
                        // A script that fails to compile or run is reported by the engine; the others still load.
                        _engine.LoadFile($"{MobilesDirectory}/{file}");
                    }
                    catch (FileNotFoundException exception)
                    {
                        _logger.Warning(exception, "Mobile script {File} disappeared before it was loaded", file);
                    }
                }
            }
        );

        await _loop.PostAsync(work);
        await work.Completion;
        _logger.Information("Loaded {Count} mobile scripts", files.Count);
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public void Think(MobileEntity npc)
    {
        if (ScriptOf(npc) is not { } script)
        {
            return;
        }

        var result = _engine.CallMember(script, "on_think", (long)npc.Id.Value);

        if (result.Kind == ScriptResultKind.Suspended && _warnedWait.Add(script))
        {
            _logger.Warning(
                "Mobile script {Script}: on_think called wait(); a think must not wait, keep the timing in the script",
                script
            );
        }
    }

    private string? ScriptOf(MobileEntity npc)
    {
        return npc.TemplateId is { } id && _templates.TryGet(id, out var template) ? template.ScriptId : null;
    }
}
