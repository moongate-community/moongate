using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Core.Interfaces.Events;

namespace Moongate.Scripting.Data.Events;

/// <summary>
///     Published on the event bus each time a script fails; the failing coroutine is already dead.
/// </summary>
public sealed record ScriptErrorEvent(ScriptErrorInfo Error) : IMoongateEvent;
