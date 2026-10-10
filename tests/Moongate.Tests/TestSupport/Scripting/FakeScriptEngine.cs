using Lua;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Interfaces;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>
///     An IScriptEngine that records what was asked of it and throws whatever a test tells it to.
/// </summary>
public sealed class FakeScriptEngine : IScriptEngine
{
    /// <summary>
    ///     Gets the paths passed to <see cref="LoadFile" />, in order.
    /// </summary>
    public List<string> Loaded { get; } = [];

    /// <summary>
    ///     Gets the paths passed to <see cref="Invalidate" />, in order.
    /// </summary>
    public List<string> Invalidated { get; } = [];

    /// <summary>
    ///     Gets or sets the exception <see cref="LoadFile" /> throws instead of loading.
    /// </summary>
    public Exception? LoadFileThrows { get; set; }

    /// <summary>
    ///     Gets or sets the snapshot <see cref="GetMetrics" /> returns; null derives one from <see cref="Loaded" />.
    /// </summary>
    public ScriptExecutionMetrics? Metrics { get; set; }

    /// <summary>
    ///     Gets the owner, table, function and arguments of every <see cref="CallMember" />, in order.
    /// </summary>
    public List<(string Owner, string Table, string Function, object?[] Args)> MemberCalls { get; } = [];

    /// <summary>
    ///     Gets or sets what <see cref="CallMember" /> returns.
    /// </summary>
    public ScriptResult MemberResult { get; set; } = ScriptResult.Completed([]);

    /// <inheritdoc />
    public ScriptResult Call(string functionName, params object?[] args)
    {
        return ScriptResult.Completed([]);
    }

    /// <inheritdoc />
    public ScriptResult CallMember(string owner, string table, string function, params object?[] args)
    {
        MemberCalls.Add((owner, table, function, args));

        return MemberResult;
    }

    /// <summary>
    ///     Gets the table and function pairs <see cref="HasMember" /> knows; null knows every one.
    /// </summary>
    public HashSet<(string Table, string Function)>? Members { get; set; }

    /// <inheritdoc />
    public bool HasMember(string table, string function)
    {
        return Members is null || Members.Contains((table, function));
    }

    /// <inheritdoc />
    public List<(string Owner, LuaFunction Function, object?[] Args)> FunctionCalls { get; } = [];

    public bool IsRunningScript { get; set; }

    public string? CurrentScript { get; set; }

    public ScriptResult CallFunction(string owner, LuaFunction function, params object?[] args)
    {
        FunctionCalls.Add((owner, function, args));

        return MemberResult;
    }

    public ScriptExecutionMetrics GetMetrics()
    {
        return Metrics ?? new ScriptExecutionMetrics(Loaded.Count, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    /// <inheritdoc />
    public void Invalidate(string relativePath)
    {
        Invalidated.Add(relativePath);
    }

    /// <inheritdoc />
    public void LoadFile(string relativePath)
    {
        Loaded.Add(relativePath);

        if (LoadFileThrows is not null)
        {
            throw LoadFileThrows;
        }
    }
}
