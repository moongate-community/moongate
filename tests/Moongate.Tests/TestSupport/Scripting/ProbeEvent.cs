using Moongate.Server.Core.Interfaces.Events;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>
///     A bus event published by the script event tests.
/// </summary>
public sealed record ProbeEvent(string Name, int Value) : IMoongateEvent;
