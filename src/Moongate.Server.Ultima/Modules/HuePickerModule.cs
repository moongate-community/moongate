using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>hue_picker</c> Lua module: shows a player the client's hue picker and runs a function with the hue it
///     picked, as dyes used on a dye tub do; <c>hue_picker.open(user, 0x0FAB, function(hue) ... end)</c>.
/// </summary>
[ScriptModule("hue_picker", "Shows a player the client's hue picker and runs a function with the hue it picked.")]
public sealed class HuePickerModule
{
    private const string AnonymousOwner = "hue_picker";

    private readonly IHuePickerService _pickers;
    private readonly ISessionService _sessions;
    private readonly Lazy<IScriptEngine> _engine;
    private readonly IGameLoopService _loop;

    public HuePickerModule(
        IHuePickerService pickers,
        ISessionService sessions,
        Lazy<IScriptEngine> engine,
        IGameLoopService loop
    )
    {
        _pickers = pickers;
        _sessions = sessions;
        _engine = engine;
        _loop = loop;
    }

    /// <summary>
    ///     Shows the player the hue picker with a graphic in it;
    ///     <c>hue_picker.open(user, item.item_id(tub), function(hue) if hue then item.set_hue(tub, hue) end end)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Shows the player the client's hue picker with a graphic (0 to 65535) in it; the function gets the hue picked, from 2 to 1001, or nil when the picker ended without an answer: another picker took its place or the player left. A player that closes the picker sends nothing, so the function may never run: check again in it what was true when the picker opened. A picker a script replaces is told nil on the next turn of the game loop. False for a graphic out of range, an NPC or a player not in the world."
    )]
    public bool Open(long player, int graphic, [ScriptParameterType("function")] LuaValue callback)
    {
        if (callback.Type != LuaValueType.Function)
        {
            throw new ArgumentException($"expected a function, got {callback.TypeToString()}", nameof(callback));
        }

        if (graphic is < 0 or > ushort.MaxValue || !TryGetSession(player, out var session))
        {
            return false;
        }

        var function = callback.Read<LuaFunction>();
        var owner = _engine.Value.CurrentScript ?? AnonymousOwner;
        _pickers.Begin(session, graphic, (_, hue) => Answer(owner, function, hue));

        return true;
    }

    // A picker ends from inside a running script when that script opens another: the function cannot run nested in
    // it, so it runs on the next turn of the game loop.
    private void Answer(string owner, LuaFunction function, int? hue)
    {
        if (_engine.Value.IsRunningScript)
        {
            _loop.TryPost(new LoopActionWorkItem(() => _engine.Value.CallFunction(owner, function, hue)));

            return;
        }

        _engine.Value.CallFunction(owner, function, hue);
    }

    private bool TryGetSession(long player, out GameSession session)
    {
        session = null!;

        return player is > 0 and <= uint.MaxValue && _sessions.TryGetByCharacterId(new Serial((uint)player), out session!);
    }
}
