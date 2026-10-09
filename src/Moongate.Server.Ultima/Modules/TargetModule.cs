using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>target</c> Lua module: gives a player the target cursor and runs a function with what it clicked, as an
///     item used on another does; <c>target.pick(user, function(picked) ... end)</c>. The function gets a table:
///     <c>{ kind = "object", serial }</c>, <c>{ kind = "location", map, x, y, z, graphic }</c> or
///     <c>{ kind = "canceled", reason }</c>, the reason being <c>canceled</c> (the player put the cursor away),
///     <c>overridden</c> (another cursor took its place) or <c>disconnected</c>.
/// </summary>
[ScriptModule("target", "Gives a player the target cursor and runs a function with what it picked.")]
public sealed class TargetModule
{
    private const string AnonymousOwner = "target";

    private readonly ITargetService _targets;
    private readonly ISessionService _sessions;
    private readonly Lazy<IScriptEngine> _engine;
    private readonly IGameLoopService _loop;

    public TargetModule(ITargetService targets, ISessionService sessions, Lazy<IScriptEngine> engine, IGameLoopService loop)
    {
        _targets = targets;
        _sessions = sessions;
        _engine = engine;
        _loop = loop;
    }

    /// <summary>
    ///     Gives the player a cursor to pick an item or a mobile;
    ///     <c>target.pick(user, function(picked) if picked.kind == "object" then ... end end)</c>. A cursor the player
    ///     already had is canceled, and its function told so.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Gives the player a cursor to pick an item or a mobile; the function gets { kind = 'object', serial } or { kind = 'canceled', reason } (reason is 'canceled' for ESC, 'overridden' when another cursor took its place, 'disconnected' when the player left). A cursor a script replaces is told canceled on the next turn of the game loop. False for an NPC or a player not in the world."
    )]
    public bool Pick(long player, [ScriptParameterType("function")] LuaValue callback)
    {
        return Begin(player, TargetCursorType.Object, callback);
    }

    /// <summary>
    ///     Gives the player a cursor to pick a place; <c>target.pick_location(user, function(picked) ... end)</c>. A
    ///     click on an item or a mobile gives that object instead.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Gives the player a cursor to pick a place; the function gets { kind = 'location', map, x, y, z, graphic } (graphic is the static picked there, which is really on the map, or 0 for the land), an object when one was clicked, or { kind = 'canceled', reason } (reason is 'canceled' for ESC, 'overridden' when another cursor took its place, 'disconnected' when the player left). A cursor a script replaces is told canceled on the next turn of the game loop. False for an NPC or a player not in the world."
    )]
    public bool PickLocation(long player, [ScriptParameterType("function")] LuaValue callback)
    {
        return Begin(player, TargetCursorType.Location, callback);
    }

    /// <summary>
    ///     Takes the target cursor away from the player; its function is told <c>canceled</c>;
    ///     <c>target.cancel(user)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Cancels the player's target cursor; its function runs with { kind = 'canceled' } on the next turn of the game loop. False for an NPC or a player not in the world."
    )]
    public bool Cancel(long player)
    {
        if (!TryGetSession(player, out var session))
        {
            return false;
        }

        _targets.Cancel(session);

        return true;
    }

    private bool Begin(long player, TargetCursorType cursor, LuaValue callback)
    {
        if (callback.Type != LuaValueType.Function)
        {
            throw new ArgumentException($"expected a function, got {callback.TypeToString()}", nameof(callback));
        }

        if (!TryGetSession(player, out var session))
        {
            return false;
        }

        var function = callback.Read<LuaFunction>();
        var owner = _engine.Value.CurrentScript ?? AnonymousOwner;
        _targets.Begin(session, cursor, TargetFlagsType.Neutral, (_, result) => Answer(owner, function, result));

        return true;
    }

    // A cursor is canceled from inside a running script when that script asks for another or cancels it: the function
    // cannot run nested in it, so it runs on the next turn of the game loop.
    private void Answer(string owner, LuaFunction function, TargetResult result)
    {
        if (_engine.Value.IsRunningScript)
        {
            _loop.TryPost(new LoopActionWorkItem(() => _engine.Value.CallFunction(owner, function, ToLua(result))));

            return;
        }

        _engine.Value.CallFunction(owner, function, ToLua(result));
    }

    private static LuaTable ToLua(TargetResult result)
    {
        var table = new LuaTable();

        switch (result.Kind)
        {
            case TargetResultType.Object:
                table["kind"] = "object";
                table["serial"] = (long)result.Serial.Value;

                break;
            case TargetResultType.Location:
                table["kind"] = "location";
                table["map"] = (int)result.Map;
                table["x"] = result.Location.X;
                table["y"] = result.Location.Y;
                table["z"] = result.Location.Z;
                // The static that was picked, so a script can tell a tree or a rock; 0 for the land.
                table["graphic"] = result.Graphic;

                break;
            default:
                table["kind"] = "canceled";
                // Why: the player put the cursor away, another cursor took its place, or the player left.
                table["reason"] = result.CancelReason switch
                {
                    TargetCancelType.Overridden   => "overridden",
                    TargetCancelType.Disconnected => "disconnected",
                    _                             => "canceled"
                };

                break;
        }

        return table;
    }

    private bool TryGetSession(long player, out GameSession session)
    {
        // Safe: out parameter; callers read it only when the method returns true.
        session = null!;

        // Safe: the out value is only used when the lookup succeeds.
        return player is > 0 and <= uint.MaxValue && _sessions.TryGetByCharacterId(new Serial((uint)player), out session!);
    }
}
