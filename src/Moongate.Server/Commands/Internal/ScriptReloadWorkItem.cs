using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Server.Commands.Internal;

/// <summary>Runs Invalidate then LoadFile on the loop and hands the outcome back to the command's thread.</summary>
internal sealed class ScriptReloadWorkItem : IGameLoopWorkItem
{
    private readonly IScriptEngine _engine;
    private readonly string _relativePath;
    private readonly TaskCompletionSource<string?> _outcome = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes with null on success, or with the error message.</summary>
    public Task<string?> Outcome => _outcome.Task;

    public ScriptReloadWorkItem(IScriptEngine engine, string relativePath)
    {
        _engine = engine;
        _relativePath = relativePath;
    }

    public void Execute()
    {
        try
        {
            _engine.Invalidate(_relativePath);
            _engine.LoadFile(_relativePath);
            _outcome.TrySetResult(null);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FileNotFoundException)
        {
            _outcome.TrySetResult(exception.Message);
        }
    }
}
