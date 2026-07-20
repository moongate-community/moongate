using Moongate.Server.Abstractions.Types;

namespace Moongate.Http.Plugin.Interfaces.Assets;

/// <summary>Stores and serves the shard's visual-asset files under one directory, one file per slot.</summary>
public interface IServerAssetFileStore
{
    /// <summary>Writes the content atomically to <c>{slot}.{extension}</c>, replacing any existing file.</summary>
    Task SaveAsync(ServerAssetSlotType slot, string extension, Stream content);

    /// <summary>Opens the stored file by its recorded name, or null when it is missing.</summary>
    (Stream stream, string fileName)? TryOpen(string fileName);

    /// <summary>Deletes the stored file by name, if present.</summary>
    void Delete(string fileName);
}
