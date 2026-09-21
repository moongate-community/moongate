using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Interfaces;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>An IScriptEngine that records what was asked of it and throws whatever a test tells it to.</summary>
public sealed class FakeScriptEngine : IScriptEngine
{
    /// <summary>Gets the paths passed to <see cref="LoadFile" />, in order.</summary>
    public List<string> Loaded { get; } = [];

    /// <summary>Gets the paths passed to <see cref="Invalidate" />, in order.</summary>
    public List<string> Invalidated { get; } = [];

    /// <summary>Gets or sets the exception <see cref="LoadFile" /> throws instead of loading.</summary>
    public Exception? LoadFileThrows { get; set; }

    /// <summary>Gets or sets the snapshot <see cref="GetMetrics" /> returns; null derives one from <see cref="Loaded" />.</summary>
    public ScriptExecutionMetrics? Metrics { get; set; }

    /// <inheritdoc />
    public ScriptResult Call(string functionName, params object?[] args)
        => ScriptResult.Completed([]);

    /// <inheritdoc />
    public ScriptExecutionMetrics GetMetrics()
        => Metrics ?? new ScriptExecutionMetrics(Loaded.Count, 0, 0, 0, 0, 0, 0, 0);

    /// <inheritdoc />
    public void Invalidate(string relativePath)
        => Invalidated.Add(relativePath);

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
