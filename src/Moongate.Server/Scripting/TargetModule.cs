using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Scripting.Refs;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;
using SquidStd.Scripting.Lua.Attributes.Scripts;

namespace Moongate.Server.Scripting;

/// <summary>
/// Asks the player to click something.
/// <para>
/// A gump asks which option; a target asks which thing. There is one cursor per player, so a new
/// request supersedes any pending one and the superseded callback is invoked as cancelled — the
/// script does not have to track that itself.
/// </para>
/// </summary>
[ScriptModule("target", "Raise the player's target cursor and act on what they click.")]
public sealed class TargetModule
{
    private readonly IPlayerTargetService _targets;
    private readonly ISessionManager _sessions;
    private readonly TargetResultFactory _results;

    public TargetModule(IPlayerTargetService targets, ISessionManager sessions, TargetResultFactory results)
    {
        _targets = targets;
        _sessions = sessions;
        _results = results;
    }

    /// <summary>Takes the cursor down. Returns false when nothing was pending.</summary>
    [ScriptFunction("cancel", "Cancels a pending target cursor: target.cancel(serial).")]
    public bool Cancel(uint serial)
        => SessionFor(serial) is { } session && _targets.Cancel(session);

    /// <summary>
    /// Raises a cursor for the mobile's player. <paramref name="selection" /> is <c>"object"</c> or
    /// <c>"location"</c>. Returns false when the serial names nobody online, or when the selection
    /// is neither — refused rather than defaulted, because targeting the wrong kind of thing in
    /// silence is worse than doing nothing.
    /// </summary>
    [ScriptFunction("request", "Raises a target cursor: target.request(serial, selection, on_target).")]
    public bool Request(uint serial, string selection, Closure onTarget)
    {
        if (!ScriptEnums.TryResolve<TargetSelectionType>(selection, out var parsed))
        {
            return false;
        }

        if (SessionFor(serial) is not { } session)
        {
            return false;
        }

        _targets.Request(session, parsed, result => onTarget.Call(_results.ToTable(result)));

        return true;
    }

    private PlayerSession? SessionFor(uint serial)
    {
        var id = (Serial)serial;

        return _sessions.All.FirstOrDefault(session => session.Character?.Id == id);
    }
}
