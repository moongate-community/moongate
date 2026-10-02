using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>target</c> Lua module: gives a player the target cursor and runs a function with what it clicked, as an
///     item used on another does; <c>target.pick(user, function(picked) ... end)</c>. The function gets a table:
///     <c>{ kind = "object", serial }</c>, <c>{ kind = "location", map, x, y, z }</c> or <c>{ kind = "canceled" }</c>.
/// </summary>
[ScriptModule("target", "Gives a player the target cursor and runs a function with what it picked.")]
public sealed class TargetModule
{
    private const string AnonymousOwner = "target";

    private readonly ITargetService _targets;
    private readonly ISessionService _sessions;
    private readonly Lazy<IScriptEngine> _engine;

    public TargetModule(ITargetService targets, ISessionService sessions, Lazy<IScriptEngine> engine)
    {
        _targets = targets;
        _sessions = sessions;
        _engine = engine;
    }

    /// <summary>
    ///     Gives the player a cursor to pick an item or a mobile; <c>target.pick(user, function(picked) if picked.kind ==
    ///     "object" then ... end end)</c>. A cursor the player already had is canceled, and its function told so.
    /// </summary>
    [ScriptFunction(helpText: "Gives the player a cursor to pick an item or a mobile; the function gets { kind = 'object', serial } or { kind = 'canceled' }. False for an NPC or a player not in the world.")]
    public bool Pick(long player, LuaValue callback)
    {
        return Begin(player, TargetCursorType.Object, callback);
    }

    /// <summary>
    ///     Gives the player a cursor to pick a place; <c>target.pick_location(user, function(picked) ... end)</c>. A
    ///     click on an item or a mobile gives that object instead.
    /// </summary>
    [ScriptFunction(helpText: "Gives the player a cursor to pick a place; the function gets { kind = 'location', map, x, y, z }, an object when one was clicked, or { kind = 'canceled' }. False for an NPC or a player not in the world.")]
    public bool PickLocation(long player, LuaValue callback)
    {
        return Begin(player, TargetCursorType.Location, callback);
    }

    /// <summary>
    ///     Takes the target cursor away from the player; its function is told <c>canceled</c>;
    ///     <c>target.cancel(user)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Cancels the player's target cursor; false for an NPC or a player not in the world.")]
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
        // The function belongs with the script that asked: reloading it ends what it left waiting.
        var owner = _engine.Value.CurrentScript ?? AnonymousOwner;
        _targets.Begin(session, cursor, TargetFlagsType.Neutral, (_, result) => _engine.Value.CallFunction(owner, function, ToLua(result)));

        return true;
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

                break;
            default:
                table["kind"] = "canceled";

                break;
        }

        return table;
    }

    private bool TryGetSession(long player, out GameSession session)
    {
        session = null!;

        return player is > 0 and <= uint.MaxValue && _sessions.TryGetByCharacterId(new Serial((uint)player), out session!);
    }
}
