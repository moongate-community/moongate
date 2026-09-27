using Moongate.Server.Core.Interfaces.Events;

namespace Moongate.Scripting.Data.Scripts;

/// <summary>
///     One bus event published to Lua: the name scripts subscribe with and how to subscribe to the bus for it.
/// </summary>
/// <param name="Name">
///     The snake_case name scripts pass to <c>events.on</c>.
/// </param>
/// <param name="EventType">
///     The bus event type; one registration per type.
/// </param>
/// <param name="Subscribe">
///     Subscribes to the bus. For every published event it calls the given delivery action with a deferred mapping
///     that returns the values Lua receives; the returned handle ends the subscription.
/// </param>
public sealed record ScriptEventRegistration(
    string Name,
    Type EventType,
    Func<IMoongateEventBus, Action<Func<IReadOnlyDictionary<string, object?>>>, IDisposable> Subscribe
);
