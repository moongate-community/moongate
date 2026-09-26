using Lua;

namespace Moongate.Scripting.Internal;

/// <summary>
///     Reads, compiles and runs script files, remembering which are loaded so a file runs once until invalidated.
/// </summary>
internal sealed class ScriptFileLoader
{
    private readonly LuaState _state;
    private readonly string _root;
    private readonly Dictionary<string, LuaValue[]> _loaded = new(StringComparer.Ordinal);

    /// <summary>
    ///     The file whose chunk is executing right now, or null between loads. Timers and coroutines record it as their owner.
    /// </summary>
    public string? CurrentFile { get; private set; }

    public IReadOnlyCollection<string> LoadedFiles => _loaded.Keys;
    public int FilesLoaded { get; private set; }

    public ScriptFileLoader(LuaState state, string scriptsDirectory)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
        _root = Path.GetFullPath(scriptsDirectory);
    }

    /// <summary>
    ///     Forgets the file and evicts its require() entry. Returns false when the file was not loaded.
    /// </summary>
    public bool Invalidate(string relativePath)
    {
        var key = Normalize(relativePath);
        _state.LoadedModules[ScriptDirectoryModuleLoader.ToModuleName(key)] = LuaValue.Nil;

        return _loaded.Remove(key);
    }

    /// <summary>
    ///     Runs the file, or returns the values of the previous run when it is already loaded.
    /// </summary>
    /// <param name="relativePath">
    ///     Path relative to the scripts directory.
    /// </param>
    /// <param name="cancellationToken">
    ///     The budget's token for the chunk: the VM checks it per instruction, so a runaway file stops.
    ///     A
    ///     <c>
    ///         require
    ///     </c>
    ///     inside the chunk runs in the same unit and goes through the same token.
    /// </param>
    public LuaValue[] Load(string relativePath, CancellationToken cancellationToken)
    {
        var key = Normalize(relativePath);

        if (_loaded.TryGetValue(key, out var previous))
        {
            return previous;
        }

        var full = ScriptDirectoryModuleLoader.ResolvePath(_root, key);

        if (!File.Exists(full))
        {
            throw new FileNotFoundException($"script '{key}' not found under the scripts directory", full);
        }

        var source = File.ReadAllText(full);
        var closure = _state.Load(source.AsSpan(), key, _state.Environment);
        var previousFile = CurrentFile;
        CurrentFile = key;

        try
        {
            var result = SyncValueTask.Run(_state.ExecuteAsync(closure, cancellationToken));
            _loaded[key] = result;
            FilesLoaded++;

            return result;
        }
        finally
        {
            CurrentFile = previousFile;
        }
    }

    /// <summary>
    ///     Reduces a path to the one spelling used as a key everywhere: forward slashes, no leading separator
    ///     and no leading "./", so "./ai/guard.lua", ".\ai\guard.lua" and "ai/guard.lua" are one file. A "../"
    ///     segment is left alone for the resolver to refuse.
    /// </summary>
    internal static string Normalize(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');

        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..].TrimStart('/');
        }

        return normalized;
    }
}
