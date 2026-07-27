using Moongate.Http.Plugin.Interfaces.Assets;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Http.Plugin.Services.Assets;

public sealed class ServerAssetFileStore : IServerAssetFileStore
{
    private readonly string _directory;

    public ServerAssetFileStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public async Task SaveAsync(ServerAssetSlotType slot, string extension, Stream content)
    {
        var fileName = $"{slot}.{extension}";
        var finalPath = Path.Combine(_directory, fileName);
        var tempPath = finalPath + ".tmp";

        await using (var file = File.Create(tempPath))
        {
            await content.CopyToAsync(file);
        }

        File.Move(tempPath, finalPath, overwrite: true);
    }

    public (Stream stream, string fileName)? TryOpen(string fileName)
    {
        var path = Path.Combine(_directory, fileName);

        return File.Exists(path)
            ? (File.OpenRead(path), fileName)
            : null;
    }

    public void Delete(string fileName)
    {
        var path = Path.Combine(_directory, fileName);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
