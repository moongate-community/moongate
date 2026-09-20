namespace Moongate.Tests.TestSupport.Persistence;

public sealed class TemporaryPersistenceDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "moongate-" + Guid.NewGuid());

    public TemporaryPersistenceDirectory()
    {
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        Directory.Delete(Path, true);
    }
}
