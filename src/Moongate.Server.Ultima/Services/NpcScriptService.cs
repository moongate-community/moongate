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
///     that waits in it is warned once. Once stopped, before the script engine, it calls nothing.
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

    private bool _stopped;

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
                        _engine.LoadFile($"{MobilesDirectory}/{file}");
                    }
                    catch (FileNotFoundException exception)
                    {
                        _logger.Warning(exception, "Mobile script {File} disappeared before it was loaded", file);
                    }
                    catch (InvalidOperationException)
                    {
                        // The engine has reported the broken script; the server starts with the others.
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
        _stopped = true;

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

    /// <summary>
    ///     Calls <paramref name="function" /> of the NPC's mobile script with its serial followed by
    ///     <paramref name="args" />; <see cref="ScriptResult.Missing" /> when it has no script, the script lacks the
    ///     function, or the scripts have stopped.
    /// </summary>
    public ScriptResult Run(MobileEntity npc, string function, params object?[] args)
    {
        if (_stopped || ScriptOf(npc) is not { } script)
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

    private string? ScriptOf(MobileEntity npc)
    {
        return npc.TemplateId is { } id && _templates.TryGet(id, out var template) ? template.ScriptId : null;
    }
}
