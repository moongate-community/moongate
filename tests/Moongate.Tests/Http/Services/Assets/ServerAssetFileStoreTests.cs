using Moongate.Http.Plugin.Services.Assets;
using Moongate.Server.Abstractions.Types;
using Xunit;

namespace Moongate.Tests.Http.Services.Assets;

public sealed class ServerAssetFileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mg-assets-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Save_ThenOpen_RoundTripsBytes()
    {
        var store = new ServerAssetFileStore(_dir);
        var bytes = new byte[] { 1, 2, 3, 4 };

        await store.SaveAsync(ServerAssetSlotType.Logo, "png", new MemoryStream(bytes));

        var opened = store.TryOpen("Logo.png");
        Assert.NotNull(opened);
        using var ms = new MemoryStream();
        await opened!.Value.stream.CopyToAsync(ms);
        opened.Value.stream.Dispose();
        Assert.Equal(bytes, ms.ToArray());
    }

    [Fact]
    public void TryOpen_MissingFile_ReturnsNull()
    {
        var store = new ServerAssetFileStore(_dir);

        Assert.Null(store.TryOpen("Logo.png"));
    }

    [Fact]
    public async Task Save_OverwritesPreviousSlotFile()
    {
        var store = new ServerAssetFileStore(_dir);
        await store.SaveAsync(ServerAssetSlotType.Logo, "png", new MemoryStream(new byte[] { 1 }));
        await store.SaveAsync(ServerAssetSlotType.Logo, "png", new MemoryStream(new byte[] { 2, 2 }));

        var opened = store.TryOpen("Logo.png")!.Value;
        using var ms = new MemoryStream();
        await opened.stream.CopyToAsync(ms);
        opened.stream.Dispose();
        Assert.Equal(new byte[] { 2, 2 }, ms.ToArray());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
