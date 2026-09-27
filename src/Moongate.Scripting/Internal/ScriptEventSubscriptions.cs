using Lua;
using Moongate.Scripting.Data.Scripts;

namespace Moongate.Scripting.Internal;

/// <summary>
///     The Lua functions subscribed to each script event, in subscription order. Lua changes it on the game loop, while
///     <see cref="HasSubscribers" /> is asked from the publishing thread, so every member takes the same lock.
/// </summary>
internal sealed class ScriptEventSubscriptions
{
    private readonly Lock _gate = new();
    private readonly List<ScriptEventSubscription> _subscriptions = [];
    private readonly Dictionary<string, string> _eventByHandle = new(StringComparer.Ordinal);
    private long _nextHandle;

    /// <summary>
    ///     Subscribes <paramref name="function" /> to <paramref name="eventName" /> on behalf of <paramref name="owner" />.
    /// </summary>
    /// <returns>
    ///     The handle for <see cref="Remove" />.
    /// </returns>
    public string Add(string eventName, string owner, LuaFunction function)
    {
        lock (_gate)
        {
            var handle = "lua-event:" + ++_nextHandle;
            _subscriptions.Add(new(handle, owner, function));
            _eventByHandle[handle] = eventName;

            return handle;
        }
    }

    /// <summary>
    ///     Removes every subscription.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _subscriptions.Clear();
            _eventByHandle.Clear();
        }
    }

    /// <summary>
    ///     Gets whether any Lua function is subscribed to <paramref name="eventName" />.
    /// </summary>
    public bool HasSubscribers(string eventName)
    {
        lock (_gate)
        {
            return _eventByHandle.ContainsValue(eventName);
        }
    }

    /// <summary>
    ///     Removes one subscription.
    /// </summary>
    /// <returns>
    ///     False when no subscription has <paramref name="handle" />.
    /// </returns>
    public bool Remove(string handle)
    {
        lock (_gate)
        {
            if (!_eventByHandle.Remove(handle))
            {
                return false;
            }

            _subscriptions.RemoveAll(subscription => subscription.Handle == handle);

            return true;
        }
    }

    /// <summary>
    ///     Removes every subscription <paramref name="owner" /> made.
    /// </summary>
    public void RemoveOwner(string owner)
    {
        lock (_gate)
        {
            foreach (var subscription in _subscriptions.Where(subscription => subscription.Owner == owner))
            {
                _eventByHandle.Remove(subscription.Handle);
            }

            _subscriptions.RemoveAll(subscription => subscription.Owner == owner);
        }
    }

    /// <summary>
    ///     Returns a copy of the subscriptions to <paramref name="eventName" />, in subscription order, so a handler that
    ///     subscribes or unsubscribes changes the next delivery, not the current one.
    /// </summary>
    public IReadOnlyList<ScriptEventSubscription> Snapshot(string eventName)
    {
        lock (_gate)
        {
            return _subscriptions.Where(subscription => _eventByHandle[subscription.Handle] == eventName).ToArray();
        }
    }
}
