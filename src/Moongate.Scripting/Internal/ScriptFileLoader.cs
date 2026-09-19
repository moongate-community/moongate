using Lua;

namespace Moongate.Scripting.Internal;

/// <summary>Reads, compiles and runs script files, remembering which are loaded so a file runs once until invalidated.</summary>
internal sealed class ScriptFileLoader
{
    private readonly LuaState _state;
    private readonly string _root;
    private readonly Dictionary<string, LuaValue[]> _loaded = new(StringComparer.Ordinal);

    /// <summary>The file whose chunk is executing right now, or null between loads. Timers and coroutines record it as their owner.</summary>
    public string? CurrentFile { get; private set; }

    public IReadOnlyCollection<string> LoadedFiles => _loaded.Keys;
    public int FilesLoaded { get; private set; }

    public ScriptFileLoader(LuaState state, string scriptsDirectory)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
        _root = Path.GetFullPath(scriptsDirectory);
    }

    /// <summary>Runs the file, or returns the values of the previous run when it is already loaded.</summary>
    public LuaValue[] Load(string relativePath)
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
            var result = SyncValueTask.Run(_state.ExecuteAsync(closure, default));
            _loaded[key] = result;
            FilesLoaded++;

            return result;
        }
        finally
        {
            CurrentFile = previousFile;
        }
    }

    /// <summary>Forgets the file and evicts its require() entry. Returns false when the file was not loaded.</summary>
    public bool Invalidate(string relativePath)
    {
        var key = Normalize(relativePath);
        var moduleName = key.EndsWith(".lua", StringComparison.Ordinal) ? key[..^4].Replace('/', '.') : key.Replace('/', '.');
        _state.LoadedModules[moduleName] = LuaValue.Nil;

        return _loaded.Remove(key);
    }

    internal static string Normalize(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return relativePath.Replace('\\', '/').TrimStart('/');
    }
}
