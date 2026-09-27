using Moongate.Server.Core.Interfaces.Events;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>
///     A bus event whose Lua mapping yields a value Lua cannot hold.
/// </summary>
public sealed record UnsupportedValueEvent(DateTime When) : IMoongateEvent;
