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
///     The <c>prompt</c> Lua module: asks a player for a line of text and runs a function with what it typed, as naming
///     a rune does; <c>prompt.ask(user, function(text) ... end)</c>. The function gets the text, or <c>nil</c> when the
///     player gave none.
/// </summary>
[ScriptModule("prompt", "Asks a player for a line of text and runs a function with what it typed.")]
public sealed class PromptModule
{
    private const string AnonymousOwner = "prompt";

    private readonly IPromptService _prompts;
    private readonly ISessionService _sessions;
    private readonly Lazy<IScriptEngine> _engine;
    private readonly IGameLoopService _loop;

    public PromptModule(IPromptService prompts, ISessionService sessions, Lazy<IScriptEngine> engine, IGameLoopService loop)
    {
        _prompts = prompts;
        _sessions = sessions;
        _engine = engine;
        _loop = loop;
    }

    /// <summary>
    ///     Asks the player for a line of text; <c>prompt.ask(user, function(text) if text then ... end end)</c>. Say
    ///     what to type first, with <c>mobile.message</c>. A prompt the player already had ends, its function told
    ///     <c>nil</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Asks the player for a line of text, typed in the journal line; the function gets the text, up to 128 characters without the spaces around it, or nil when the player escaped, typed only spaces, was asked something else or left. Say what to type first with mobile.message. False for an NPC or a player not in the world."
    )]
    public bool Ask(long player, [ScriptParameterType("function")] LuaValue callback)
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
        _prompts.Begin(session, (_, text) => Answer(owner, function, text));

        return true;
    }

    /// <summary>
    ///     Stops waiting for the player's text; its function is told <c>nil</c>; <c>prompt.cancel(user)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Stops waiting for the player's text; false for an NPC or a player not in the world.")]
    public bool Cancel(long player)
    {
        if (!TryGetSession(player, out var session))
        {
            return false;
        }

        _prompts.Cancel(session);

        return true;
    }

    // A prompt ends from inside a running script when that script asks for another or cancels it: the function cannot
    // run nested in it, so it runs on the next turn of the game loop. So does one that ends off the loop, as when a
    // session closes while the server stops: once the loop is gone it is dropped.
    private void Answer(string owner, LuaFunction function, string? text)
    {
        if (_engine.Value.IsRunningScript || !_loop.IsOnLoopThread)
        {
            _loop.TryPost(new LoopActionWorkItem(() => _engine.Value.CallFunction(owner, function, text)));

            return;
        }

        _engine.Value.CallFunction(owner, function, text);
    }

    private bool TryGetSession(long player, out GameSession session)
    {
        // Safe: out parameter; callers read it only when the method returns true.
        session = null!;

        // Safe: the out value is only used when the lookup succeeds.
        return player is > 0 and <= uint.MaxValue && _sessions.TryGetByCharacterId(new Serial((uint)player), out session!);
    }
}
