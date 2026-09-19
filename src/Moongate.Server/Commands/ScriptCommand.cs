using Moongate.Scripting.Interfaces;
using Moongate.Server.Commands.Internal;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Commands;

/// <summary>"script reload &lt;file&gt;" re-reads one script file on the game loop; "script metrics" prints the engine's counters.</summary>
public sealed class ScriptCommand : ICommandExecutor
{
    private const string Usage = "Usage: script reload <file relative to scripts/> | script metrics";

    private readonly IScriptEngine _engine;
    private readonly IGameLoopService _gameLoop;

    /// <summary>Initializes a new instance of the <see cref="ScriptCommand"/> class.</summary>
    /// <param name="engine">Engine whose <see cref="IScriptEngine.Invalidate"/> and <see cref="IScriptEngine.LoadFile"/> run a reload, and whose <see cref="IScriptEngine.GetMetrics"/> answers "metrics".</param>
    /// <param name="gameLoop">Loop a reload is posted to, since the command runs on the caller's thread.</param>
    public ScriptCommand(IScriptEngine engine, IGameLoopService gameLoop)
    {
        _engine = engine;
        _gameLoop = gameLoop;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CommandContext context)
    {
        var arguments = context.Arguments;

        if (arguments.Length == 1 && string.Equals(arguments[0], "metrics", StringComparison.OrdinalIgnoreCase))
        {
            PrintMetrics(context);

            return;
        }

        if (arguments.Length != 2 || !string.Equals(arguments[0], "reload", StringComparison.OrdinalIgnoreCase))
        {
            context.PrintError(Usage);

            return;
        }

        var workItem = new ScriptReloadWorkItem(_engine, arguments[1]);
        await _gameLoop.PostAsync(workItem, context.CancellationToken);
        var error = await workItem.Outcome.WaitAsync(context.CancellationToken);

        if (error is null)
        {
            context.Print("Reloaded {0}", arguments[1]);
        }
        else
        {
            context.PrintError("Reload failed: {0}", error);
        }
    }

    private void PrintMetrics(CommandContext context)
    {
        // GetMetrics is the one engine member documented as callable from any thread, so no loop hop.
        var metrics = _engine.GetMetrics();
        context.Print("Files loaded: {0}", metrics.FilesLoaded);
        context.Print("Calls started: {0}", metrics.CallsStarted);
        context.Print("Coroutines resumed: {0}", metrics.CoroutinesResumed);
        context.Print("Coroutines finished: {0}", metrics.CoroutinesFinished);
        context.Print("Coroutine errors: {0}", metrics.Errors);
        context.Print("Budget aborts: {0}", metrics.BudgetAborts);
        context.Print("Active coroutines: {0}", metrics.ActiveCoroutines);
        context.Print("String cap hits: {0}", metrics.StringCapHits);
    }
}
