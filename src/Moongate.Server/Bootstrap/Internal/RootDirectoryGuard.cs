namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
///     Refuses a server root that is the directory holding the binary.
/// </summary>
internal static class RootDirectoryGuard
{
    /// <summary>
    ///     Throws when <paramref name="rootDirectory" /> and <paramref name="binaryDirectory" /> are the same directory.
    /// </summary>
    /// <param name="rootDirectory">
    ///     The resolved server root.
    /// </param>
    /// <param name="binaryDirectory">
    ///     The directory holding the running server binary.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///     The root is the directory holding the binary.
    /// </exception>
    public static void EnsureNotBinaryDirectory(string rootDirectory, string binaryDirectory)
    {
        var root = Normalize(rootDirectory);
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                             ? StringComparison.OrdinalIgnoreCase
                             : StringComparison.Ordinal;

        if (!string.Equals(root, Normalize(binaryDirectory), comparison))
        {
            return;
        }

        throw new InvalidOperationException(
            $"the root directory '{root}' is the directory holding the Moongate binary, which an upgrade replaces. " +
            "Pass --root-directory <path> or set MOONGATE_ROOT to a directory outside the installation."
        );
    }

    private static string Normalize(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
