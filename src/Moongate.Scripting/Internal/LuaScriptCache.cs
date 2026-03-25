using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Moongate.Scripting.Data.Scripts;
using MoonSharp.Interpreter;

namespace Moongate.Scripting.Internal;

/// <summary>
/// Caches compiled Lua chunks and tracks cache metrics.
/// </summary>
internal sealed class LuaScriptCache
{
    private readonly ConcurrentDictionary<string, DynValue> _compiledScripts = new();

    private int _cacheHits;
    private int _cacheMisses;

    /// <summary>
    /// Returns a cached compiled chunk or compiles and stores it on miss.
    /// </summary>
    public DynValue GetOrAddCompiledChunk(string script, Func<DynValue> compiler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        ArgumentNullException.ThrowIfNull(compiler);

        var scriptHash = GetScriptHash(script);

        if (_compiledScripts.TryGetValue(scriptHash, out var compiledChunk))
        {
            Interlocked.Increment(ref _cacheHits);

            return compiledChunk;
        }

        Interlocked.Increment(ref _cacheMisses);
        var compiled = compiler();

        if (_compiledScripts.TryAdd(scriptHash, compiled))
        {
            return compiled;
        }

        Interlocked.Increment(ref _cacheHits);

        return _compiledScripts[scriptHash];
    }

    /// <summary>
    /// Clears all cached scripts and resets metrics.
    /// </summary>
    public void Clear()
    {
        _compiledScripts.Clear();
        _cacheHits = 0;
        _cacheMisses = 0;
    }

    /// <summary>
    /// Returns current cache metrics.
    /// </summary>
    public ScriptExecutionMetrics GetMetrics()
        => new()
        {
            CacheHits = _cacheHits,
            CacheMisses = _cacheMisses,
            TotalScriptsCached = _compiledScripts.Count
        };

    private static string GetScriptHash(string script)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(script));

        return Convert.ToBase64String(hashBytes);
    }
}
