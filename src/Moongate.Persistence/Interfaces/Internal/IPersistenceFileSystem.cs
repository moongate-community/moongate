namespace Moongate.Persistence.Interfaces.Internal;

/// <summary>Owns the filesystem operations needed for durable collection publication.</summary>
internal interface IPersistenceFileSystem
{
    /// <summary>Opens a seekable stream with the requested access and sharing policy.</summary>
    Stream Open(string path, FileMode mode, FileAccess access, FileShare share);

    /// <summary>Reports whether a committed file exists.</summary>
    bool Exists(string path);

    /// <summary>Creates the collection directory and any missing parents.</summary>
    void CreateDirectory(string path);

    /// <summary>Deletes a temporary file if it exists.</summary>
    void Delete(string path);

    /// <summary>Atomically replaces a destination with a file on the same filesystem.</summary>
    void Move(string source, string destination);

    /// <summary>Flushes the stream buffers and requests durable storage before returning.</summary>
    void FlushToDisk(Stream stream);
}
