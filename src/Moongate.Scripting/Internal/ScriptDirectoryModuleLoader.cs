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

    /// <summary>Resolves a relative script path to an absolute one, refusing anything that escapes the directory, symbolic links included.</summary>
    /// <remarks>
    /// The path is used exactly as written, so a file named <c>100%25.lua</c> is that file and not
    /// <c>100%.lua</c>. Every existing entry along the path that is a link is followed to its final
    /// target, which must also lie inside the scripts directory.
    /// </remarks>
    public static string ResolvePath(string scriptsDirectory, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var root = Path.GetFullPath(scriptsDirectory);

        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException($"'{relativePath}' is absolute; script paths are relative to the scripts directory.");
        }

        var full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

        if (!full.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"'{relativePath}' resolves outside the scripts directory.");
        }

        EnsureNoLinkEscapes(root, rootWithSeparator, full, relativePath);

        return full;
    }

    /// <summary>Follows every link along <paramref name="full"/> below the root and refuses one whose final target leaves the directory.</summary>
    private static void EnsureNoLinkEscapes(string root, string rootWithSeparator, string full, string relativePath)
    {
        var current = root;

        foreach (var segment in full[rootWithSeparator.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo entry = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);

            if (!entry.Exists || entry.LinkTarget is null)
            {
                continue;
            }

            var target = entry.ResolveLinkTarget(returnFinalTarget: true)?.FullName;

            if (target is null || (!target.StartsWith(rootWithSeparator, StringComparison.Ordinal) && !string.Equals(target, root, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"'{relativePath}' resolves outside the scripts directory through a link.");
            }
        }
    }

    /// <summary>Maps a normalized relative path back to the require() name it is served under: "common/dialogue.lua" becomes "common.dialogue". The inverse of the name-to-path mapping.</summary>
    public static string ToModuleName(string normalizedRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedRelativePath);
        var withoutExtension = normalizedRelativePath.EndsWith(".lua", StringComparison.Ordinal)
            ? normalizedRelativePath[..^4]
            : normalizedRelativePath;

        return withoutExtension.Replace('/', '.');
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
