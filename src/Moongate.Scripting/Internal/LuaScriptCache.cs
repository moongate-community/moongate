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
    private readonly ConcurrentDictionary<string, DynValue> _compiledScriptsByContentHash = new();
    private readonly ConcurrentDictionary<string, (string ScriptHash, DynValue Chunk)> _compiledScriptsByFilePath =
        new(StringComparer.OrdinalIgnoreCase);

    private int _cacheHits;
    private int _cacheMisses;

    /// <summary>
    /// Returns a cached compiled chunk or compiles and stores it on miss.
    /// </summary>
    public DynValue GetOrAddCompiledChunk(string script, string? filePath, Func<DynValue> compiler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        ArgumentNullException.ThrowIfNull(compiler);

        var scriptHash = GetScriptHash(script);

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            var normalizedFilePath = NormalizePath(filePath);

            if (_compiledScriptsByFilePath.TryGetValue(normalizedFilePath, out var cachedFileEntry)
                && cachedFileEntry.ScriptHash == scriptHash)
            {
                Interlocked.Increment(ref _cacheHits);

                return cachedFileEntry.Chunk;
            }

            Interlocked.Increment(ref _cacheMisses);
            var compiledFileChunk = compiler();
            _compiledScriptsByFilePath[normalizedFilePath] = (scriptHash, compiledFileChunk);

            return compiledFileChunk;
        }

        if (_compiledScriptsByContentHash.TryGetValue(scriptHash, out var compiledChunk))
        {
            Interlocked.Increment(ref _cacheHits);

            return compiledChunk;
        }

        Interlocked.Increment(ref _cacheMisses);
        var compiled = compiler();

        if (_compiledScriptsByContentHash.TryAdd(scriptHash, compiled))
        {
            return compiled;
        }

        Interlocked.Increment(ref _cacheHits);

        return _compiledScriptsByContentHash[scriptHash];
    }

    /// <summary>
    /// Invalidates one cached compiled chunk for a script file.
    /// </summary>
    public bool Invalidate(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return _compiledScriptsByFilePath.TryRemove(NormalizePath(filePath), out _);
    }

    /// <summary>
    /// Clears all cached scripts and resets metrics.
    /// </summary>
    public void Clear()
    {
        _compiledScriptsByContentHash.Clear();
        _compiledScriptsByFilePath.Clear();
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
            TotalScriptsCached = _compiledScriptsByContentHash.Count + _compiledScriptsByFilePath.Count
        };

    private static string GetScriptHash(string script)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(script));

        return Convert.ToBase64String(hashBytes);
    }

    private static string NormalizePath(string filePath)
        => Path.GetFullPath(filePath);
}
