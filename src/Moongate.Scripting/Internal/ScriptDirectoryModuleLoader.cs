using Lua;

namespace Moongate.Scripting.Internal;

/// <summary>Serves require() from the scripts directory only. "common.dialogue" maps to "common/dialogue.lua".</summary>
internal sealed class ScriptDirectoryModuleLoader : ILuaModuleLoader
{
    private readonly string _root;

    public ScriptDirectoryModuleLoader(string scriptsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptsDirectory);
        _root = Path.GetFullPath(scriptsDirectory);
    }

    public bool Exists(string moduleName)
    {
        return TryResolve(moduleName, out var path) && File.Exists(path);
    }

    public ValueTask<LuaModule> LoadAsync(string moduleName, CancellationToken cancellationToken)
    {
        if (!TryResolve(moduleName, out var path) || !File.Exists(path))
        {
            throw new FileNotFoundException($"module '{moduleName}' not found under the scripts directory", moduleName);
        }

        var text = File.ReadAllText(path);

        return new ValueTask<LuaModule>(new LuaModule(ToRelative(moduleName), text));
    }

    /// <summary>Resolves a relative script path to an absolute one, refusing anything that escapes the directory.</summary>
    public static string ResolvePath(string scriptsDirectory, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var root = Path.GetFullPath(scriptsDirectory);
        var decoded = Uri.UnescapeDataString(relativePath);

        if (Path.IsPathRooted(decoded))
        {
            throw new InvalidOperationException($"'{relativePath}' is absolute; script paths are relative to the scripts directory.");
        }

        var full = Path.GetFullPath(Path.Combine(root, decoded.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

        if (!full.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"'{relativePath}' resolves outside the scripts directory.");
        }

        return full;
    }

    private static string ToRelative(string moduleName)
    {
        return moduleName.Replace('.', '/') + ".lua";
    }

    private bool TryResolve(string moduleName, out string path)
    {
        try
        {
            path = ResolvePath(_root, ToRelative(moduleName));

            return true;
        }
        catch (InvalidOperationException)
        {
            path = "";

            return false;
        }
    }
}
