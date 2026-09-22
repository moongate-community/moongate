namespace Moongate.UoxItemConverter.Tests.TestSupport;

/// <summary>A temporary source/destination/loot-destination directory triple for one test run.</summary>
internal sealed class ConverterTestDirectories : IDisposable
{
    public string SourceDirectory { get; }
    public string DestinationDirectory { get; }
    public string LootDestinationDirectory { get; }

    public ConverterTestDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "moongate-uox-converter-" + Guid.NewGuid().ToString("N"));
        SourceDirectory = Path.Combine(root, "source");
        DestinationDirectory = Path.Combine(root, "destination");
        LootDestinationDirectory = Path.Combine(root, "loot-destination");
        Directory.CreateDirectory(SourceDirectory);
    }

    /// <summary>Writes one <c>.dfn</c> source file under <see cref="SourceDirectory" />.</summary>
    public string WriteSource(string relativePath, string content)
    {
        var path = Path.Combine(SourceDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);

        return path;
    }

    public void Dispose()
    {
        var root = Path.GetDirectoryName(SourceDirectory)!;

        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
