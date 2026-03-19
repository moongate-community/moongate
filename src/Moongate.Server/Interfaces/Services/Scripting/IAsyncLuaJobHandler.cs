namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Represents a named asynchronous job callable from Lua.
/// </summary>
public interface IAsyncLuaJobHandler
{
    /// <summary>
    /// Gets the stable job name used by Lua to request this handler.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the background work for the provided payload.
    /// </summary>
    Task<Dictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> payload,
        CancellationToken cancellationToken
    );
}
