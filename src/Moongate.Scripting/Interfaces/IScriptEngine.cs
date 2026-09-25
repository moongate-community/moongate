using System.Diagnostics.CodeAnalysis;
using Moongate.Scripting.Data.Scripts;

namespace Moongate.Scripting.Interfaces;

/// <summary>
///     The Lua runtime as the rest of the server sees it. Every member runs on the game loop thread.
/// </summary>
public interface IScriptEngine
{
    /// <summary>
    ///     Calls a global Lua function as a coroutine.
    /// </summary>
    /// <param name="functionName">
    ///     Name of a global function.
    /// </param>
    /// <param name="args">
    ///     Arguments converted with the same rules as module functions.
    /// </param>
    /// <returns>
    ///     Completed with the returned values, Suspended if the function called
    ///     <c>
    ///         wait
    ///     </c>
    ///     , or Failed with the error.
    /// </returns>
    /// <remarks>
    ///     A function whose return value the caller needs must not call
    ///     <c>
    ///         wait
    ///     </c>
    ///     : a suspended call returns no values.
    ///     A call from the host outside any file load is owned by the bootstrap file, so invalidating it cancels such coroutines.
    /// </remarks>
    [SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "'Call' is the domain term for invoking a Lua function; it is not a C# keyword."
    )]
    ScriptResult Call(string functionName, params object?[] args);

    /// <summary>
    ///     Returns a snapshot of the execution counters. Unlike the other members this may be called from any thread;
    ///     diagnostics collectors run off the loop.
    /// </summary>
    ScriptExecutionMetrics GetMetrics();

    /// <summary>
    ///     Forgets a loaded file, cancels the coroutines and timers it owns, and evicts it from require's cache.
    /// </summary>
    /// <param name="relativePath">
    ///     Path relative to the scripts directory.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///     Called off the loop thread, or the engine has not started.
    /// </exception>
    /// <remarks>
    ///     Timers created by a required module's top-level code are owned by the file that first required it,
    ///     so invalidating the module evicts it from the require cache but cancels nothing; invalidate the
    ///     requiring file for that.
    /// </remarks>
    void Invalidate(string relativePath);

    /// <summary>
    ///     Executes a file under the scripts directory, reading it from disk if it was never loaded or was invalidated.
    /// </summary>
    /// <param name="relativePath">
    ///     Path relative to the scripts directory, with forward slashes, such as
    ///     <c>
    ///         ai/guard.lua
    ///     </c>
    ///     .
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///     Called off the loop thread, or the path leaves the scripts directory.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    ///     The file does not exist.
    /// </exception>
    void LoadFile(string relativePath);
}
