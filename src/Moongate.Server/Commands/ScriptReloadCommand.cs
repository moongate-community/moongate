using Moongate.Scripting.Interfaces;
using Moongate.Server.Commands.Internal;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Commands;

/// <summary>"script reload &lt;file&gt;": re-reads one script file on the game loop.</summary>
public sealed class ScriptReloadCommand : ICommandExecutor
{
    private readonly IScriptEngine _engine;
    private readonly IGameLoopService _gameLoop;

    /// <summary>Initializes a new instance of the <see cref="ScriptReloadCommand"/> class.</summary>
    /// <param name="engine">Engine whose <see cref="IScriptEngine.Invalidate"/> and <see cref="IScriptEngine.LoadFile"/> run the reload.</param>
    /// <param name="gameLoop">Loop the reload is posted to, since the command runs on the caller's thread.</param>
    public ScriptReloadCommand(IScriptEngine engine, IGameLoopService gameLoop)
    {
        _engine = engine;
        _gameLoop = gameLoop;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length != 2 || !string.Equals(context.Arguments[0], "reload", StringComparison.OrdinalIgnoreCase))
        {
            context.PrintError("Usage: script reload <file relative to scripts/>");

            return;
        }

        var workItem = new ScriptReloadWorkItem(_engine, context.Arguments[1]);
        await _gameLoop.PostAsync(workItem, context.CancellationToken);
        var error = await workItem.Outcome.WaitAsync(context.CancellationToken);

        if (error is null)
        {
            context.Print("Reloaded {0}", context.Arguments[1]);
        }
        else
        {
            context.PrintError("Reload failed: {0}", error);
        }
    }
}
