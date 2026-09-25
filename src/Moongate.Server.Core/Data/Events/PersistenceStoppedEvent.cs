using Moongate.Server.Core.Interfaces.Events;

namespace Moongate.Server.Core.Data.Events;

/// <summary>
///     Published after an initialized persistence owner is disposed, before the container is disposed.
/// </summary>
public sealed record PersistenceStoppedEvent : IMoongateEvent;
