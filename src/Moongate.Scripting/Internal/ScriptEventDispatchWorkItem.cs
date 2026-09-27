using Lua;
using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Scripting.Internal;

/// <summary>
///     Runs the Lua handlers of one published event on the loop thread.
/// </summary>
internal sealed class ScriptEventDispatchWorkItem : IGameLoopWorkItem
{
    private readonly Action<string, IReadOnlyList<KeyValuePair<string, LuaValue>>> _dispatch;
    private readonly string _eventName;
    private readonly IReadOnlyList<KeyValuePair<string, LuaValue>> _values;

    public ScriptEventDispatchWorkItem(
        Action<string, IReadOnlyList<KeyValuePair<string, LuaValue>>> dispatch,
        string eventName,
        IReadOnlyList<KeyValuePair<string, LuaValue>> values
    )
    {
        _dispatch = dispatch;
        _eventName = eventName;
        _values = values;
    }

    public void Execute()
    {
        _dispatch(_eventName, _values);
    }
}
