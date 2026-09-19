namespace Moongate.Scripting.Interfaces;

/// <summary>Enforces the thread on which script code may run.</summary>
public interface IScriptThreadGuard
{
    /// <summary>Throws <see cref="InvalidOperationException"/> naming <paramref name="member"/> when the current thread may not run scripts.</summary>
    void EnsureScriptThread(string member);
}
