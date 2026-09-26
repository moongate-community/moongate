using Moongate.Server.Data.Config;
using Moongate.Server.Services.Ultima;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Services.Ultima;

public sealed class UltimaDataServiceTests
{
    [Fact]
    public async Task StartAsync_MissingDirectory_ThrowsDirectoryNotFoundException()
    {
        using var directory = new TemporaryDirectory();
        var service = CreateService(Path.Combine(directory.Path, "missing"));

        await Assert.ThrowsAsync<DirectoryNotFoundException>(service.StartAsync);
    }

    [Fact]
    public async Task StartAsync_ClientWithoutTileData_ThrowsFileNotFoundException()
    {
        using var directory = new TemporaryDirectory();
        var service = CreateService(directory.Path);

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(service.StartAsync);

        Assert.Contains("tiledata.mul", exception.Message);
    }

    private static UltimaDataService CreateService(string ultimaPath)
    {
        var config = new MoongateServerConfig();
        config.Ultima.UltimaPath = ultimaPath;

        return new UltimaDataService(config);
    }
}
