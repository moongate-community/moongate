using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>
///     A module whose function runs a test-supplied callback, so a test can act while a Lua coroutine is resuming.
/// </summary>
[ScriptModule("callback")]
public sealed class CallbackModule
{
    /// <summary>
    ///     Gets or sets what <see cref="Invoke" /> runs.
    /// </summary>
    public Action? OnInvoke { get; set; }

    [ScriptFunction]
    public void Invoke()
    {
        OnInvoke?.Invoke();
    }
}
