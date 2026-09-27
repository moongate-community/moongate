using Lua;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Interfaces.Internal;
using Moongate.Scripting.Internal;

namespace Moongate.Scripting.Modules;

/// <summary>
///     Subscriptions to server events. Every handler runs as a coroutine on the game loop, so it may call wait().
/// </summary>
/// <remarks>
///     Built in: the engine constructs it, because its dependencies are engine internals. Hosts publish events with
///     <c>AddScriptEvent</c>; they never register this module.
/// </remarks>
[ScriptModule("events", "Subscribes functions to server events.")]
internal sealed class EventsModule
{
    private const string AnonymousOwner = "<anonymous>";

    private readonly ScriptEventSubscriptions _subscriptions;
    private readonly IScriptScheduler _scheduler;
    private readonly IReadOnlySet<string> _names;

    /// <param name="subscriptions">
    ///     Where subscriptions are kept, by event and owner.
    /// </param>
    /// <param name="scheduler">
    ///     Names the file subscribing.
    /// </param>
    /// <param name="names">
    ///     The registered event names; any other name is refused.
    /// </param>
    public EventsModule(ScriptEventSubscriptions subscriptions, IScriptScheduler scheduler, IReadOnlySet<string> names)
    {
        _subscriptions = subscriptions;
        _scheduler = scheduler;
        _names = names;
    }

    /// <summary>
    ///     Removes a subscription by handle.
    /// </summary>
    /// <returns>
    ///     False when no subscription has that handle.
    /// </returns>
    [ScriptFunction(helpText: "Removes a subscription by handle. Returns false when no such subscription exists.")]
    public bool Off(string handle)
    {
        return _subscriptions.Remove(handle);
    }

    /// <summary>
    ///     Subscribes <paramref name="fn" /> to the event registered as <paramref name="name" />.
    /// </summary>
    /// <returns>
    ///     A handle for <see cref="Off" />.
    /// </returns>
    [ScriptFunction(helpText: "Runs fn with the event's table every time the event happens. Returns a handle for off.")]
    public string On([ScriptParameterType("EventName")] string name, LuaValue fn)
    {
        if (!_names.Contains(name))
        {
            throw new ArgumentException($"unknown event '{name}'", nameof(name));
        }

        if (fn.Type != LuaValueType.Function)
        {
            throw new ArgumentException($"expected a function, got {fn.TypeToString()}", nameof(fn));
        }

        return _subscriptions.Add(name, _scheduler.CurrentOwner ?? AnonymousOwner, fn.Read<LuaFunction>());
    }
}
