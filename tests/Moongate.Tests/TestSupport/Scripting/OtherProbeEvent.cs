using Moongate.Server.Core.Interfaces.Events;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>
///     A second bus event type, for registrations that must not collide with <see cref="ProbeEvent" />.
/// </summary>
public sealed record OtherProbeEvent(string Name) : IMoongateEvent;
