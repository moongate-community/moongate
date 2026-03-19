namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Provides named registration and lookup for Lua background jobs.
/// </summary>
public interface IAsyncLuaJobRegistry
{
    /// <summary>
    /// Attempts to register a handler by its job name.
    /// </summary>
    bool TryRegister(IAsyncLuaJobHandler handler);

    /// <summary>
    /// Attempts to resolve a registered handler by job name.
    /// </summary>
    bool TryResolve(string jobName, out IAsyncLuaJobHandler? handler);
}
