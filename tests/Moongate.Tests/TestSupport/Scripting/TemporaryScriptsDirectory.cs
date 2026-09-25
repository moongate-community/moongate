namespace Moongate.Tests.TestSupport.Scripting;

public sealed class TemporaryScriptsDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        "moongate-scripts-" + Guid.NewGuid().ToString("N")
    );

    public TemporaryScriptsDirectory()
    {
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// Writes a file under the directory, creating parent folders. Uses forward slashes in
    /// <paramref name="relativePath" />.
    /// </summary>
    public string Write(string relativePath, string content)
    {
        var full = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);

        return full;
    }

    public void Dispose()
    {
        Directory.Delete(Path, true);
    }
}
