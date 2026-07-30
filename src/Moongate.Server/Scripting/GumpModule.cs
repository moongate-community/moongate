using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.Gumps;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Gumps;
using Moongate.Server.Scripting.Refs;
using MoonSharp.Interpreter;
using SquidStd.Scripting.Lua.Attributes.Scripts;

namespace Moongate.Server.Scripting;

/// <summary>
/// Draws gumps from Lua. A gump is described by calling the element functions on the builder handed
/// to the build function, and answered through a callback.
/// <para>
/// There is no blocking form, and there cannot be: script handlers run on the game-loop thread, so a
/// call that waited for the player's answer would stop the world. Anyone arriving from POL, where
/// <c>SendDialogGump</c> returns the pressed button, should expect the callback instead.
/// </para>
/// </summary>
[ScriptModule("gump", "Draw server-side dialogs and receive what the player did with them.")]
public sealed class GumpModule
{
    private readonly IGumpService _gumps;
    private readonly ISessionManager _sessions;
    private readonly GumpBuilderFactory _builders;

    public GumpModule(IGumpService gumps, ISessionManager sessions, GumpBuilderFactory builders)
    {
        _gumps = gumps;
        _sessions = sessions;
        _builders = builders;
    }

    /// <summary>
    /// Draws a gump for the mobile's player and sends it. Returns false when the serial names nobody
    /// online. The response callback receives a table with <c>button</c>, <c>switches</c> and
    /// <c>text</c>.
    /// </summary>
    [ScriptFunction("show", "Draws a gump for a player: gump.show(serial, id, build, on_response).")]
    public bool Show(uint serial, string gumpId, Closure build, Closure onResponse)
    {
        if (SessionFor(serial) is not { } session)
        {
            return false;
        }

        _gumps.Show(
            session,
            gumpId,
            builder => build.Call(_builders.Create(builder)),
            onResponse is null ? null : response => onResponse.Call(_builders.ToResponseTable(response))
        );

        return true;
    }

    /// <summary>Forgets a named gump for the mobile's player. Returns false when none was open.</summary>
    [ScriptFunction("close", "Closes a named gump for a player: gump.close(serial, id).")]
    public bool Close(uint serial, string gumpId)
        => SessionFor(serial) is { } session && _gumps.Close(session, gumpId);

    private PlayerSession? SessionFor(uint serial)
    {
        var id = (Serial)serial;

        return _sessions.All.FirstOrDefault(session => session.Character?.Id == id);
    }
}
