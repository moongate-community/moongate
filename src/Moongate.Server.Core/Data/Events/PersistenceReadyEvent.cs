using Moongate.Server.Core.Interfaces.Events;

namespace Moongate.Server.Core.Data.Events;

/// <summary>
///     Published after persistence initialization succeeds, before startup services begin.
/// </summary>
public sealed record PersistenceReadyEvent : IMoongateEvent;
