using Moongate.Persistence.Interfaces.Internal;

namespace Moongate.Persistence.Internal;

internal sealed class PhysicalPersistenceFileSystem : IPersistenceFileSystem
{
    public Stream Open(string path, FileMode mode, FileAccess access, FileShare share)
    {
        return new FileStream(path, mode, access, share);
    }

    public bool Exists(string path)
    {
        return File.Exists(path);
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void Delete(string path)
    {
        File.Delete(path);
    }

    public void Move(string source, string destination)
    {
        File.Move(source, destination, overwrite: true);
    }

    public void FlushToDisk(Stream stream)
    {
        ((FileStream)stream).Flush(flushToDisk: true);
    }
}
