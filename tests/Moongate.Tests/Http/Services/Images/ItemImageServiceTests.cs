using Moongate.Http.Plugin.Services.Images;
using Moongate.Http.Plugin.Services.Ultima;
using Moongate.Tests.Support;
using Moongate.Ultima.Catalog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Tests.Http.Services.Images;

[Collection("UltimaClientData")]
public class ItemImageServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_FirstCall_WritesADecodablePng()
    {
        using var fixture = ItemImageFixture.Create();
        var service = new ItemImageService(new ItemCatalog(), fixture.Directories, new UltimaReadGate());

        var path = await service.GetOrCreateAsync(ItemImageFixture.ItemId, 0);

        Assert.NotNull(path);
        Assert.True(File.Exists(path));

        using var image = await Image.LoadAsync<Bgra32>(path);

        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
    }

    [Fact]
    public async Task GetOrCreateAsync_SecondCall_ServesTheCachedFileWithoutRewritingIt()
    {
        using var fixture = ItemImageFixture.Create();
        var service = new ItemImageService(new ItemCatalog(), fixture.Directories, new UltimaReadGate());

        var first = await service.GetOrCreateAsync(ItemImageFixture.ItemId, 0);

        // Backdate the file, then check the timestamp survives. Asserting on the file rather than on a mock
        // keeps the test honest about what the cache is for: not decoding through Art twice.
        var marker = DateTime.UtcNow.AddDays(-1);
        File.SetLastWriteTimeUtc(first!, marker);

        var second = await service.GetOrCreateAsync(ItemImageFixture.ItemId, 0);

        Assert.Equal(first, second);
        Assert.Equal(marker, File.GetLastWriteTimeUtc(second!), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetOrCreateAsync_MissingArt_ReturnsNull()
    {
        using var fixture = ItemImageFixture.Create();
        var service = new ItemImageService(new ItemCatalog(), fixture.Directories, new UltimaReadGate());

        Assert.Null(await service.GetOrCreateAsync(ItemImageFixture.MissingArtItemId, 0));
    }

    [Fact]
    public async Task GetOrCreateAsync_Hued_GetsItsOwnFileWithDifferentPixels()
    {
        using var fixture = ItemImageFixture.Create();
        var service = new ItemImageService(new ItemCatalog(), fixture.Directories, new UltimaReadGate());

        var plain = await service.GetOrCreateAsync(ItemImageFixture.ItemId, 0);
        var hued = await service.GetOrCreateAsync(ItemImageFixture.ItemId, ItemImageFixture.Hue);

        Assert.NotEqual(plain, hued);

        using var plainImage = await Image.LoadAsync<Bgra32>(plain!);
        using var huedImage = await Image.LoadAsync<Bgra32>(hued!);

        Assert.NotEqual(plainImage[0, 0], huedImage[0, 0]);
    }

    [Fact]
    public async Task GetOrCreateAsync_ConcurrentCallers_AllGetTheirOwnValidPng()
    {
        // The reason the gate exists: Art holds no lock, and shares an LRU cache, non-concurrent
        // dictionaries and a static scratch buffer across calls. Parallel decodes are what corrupt it.
        using var fixture = ItemImageFixture.Create();
        var service = new ItemImageService(new ItemCatalog(), fixture.Directories, new UltimaReadGate());

        var hues = Enumerable.Range(1, 16).Select(hue => (ushort)hue).ToArray();

        var paths = await Task.WhenAll(
            hues.Select(hue => service.GetOrCreateAsync(ItemImageFixture.ItemId, hue))
        );

        Assert.Equal(hues.Length, paths.Distinct().Count());

        foreach (var path in paths)
        {
            using var image = await Image.LoadAsync<Bgra32>(path!);

            Assert.Equal(2, image.Width);
        }
    }

    [Fact]
    public async Task GetArtItemIdsAsync_ReturnsOnlyIdsThatHaveArt()
    {
        using var fixture = ItemImageFixture.Create();
        var service = new ItemImageService(new ItemCatalog(), fixture.Directories, new UltimaReadGate());

        var ids = await service.GetArtItemIdsAsync();

        Assert.Contains((uint)ItemImageFixture.ItemId, ids);
        Assert.DoesNotContain((uint)ItemImageFixture.MissingArtItemId, ids);
    }

    [Fact]
    public void IsReady_TrueOnceTheClientFilesAreLoaded()
    {
        using var fixture = ItemImageFixture.Create();
        var service = new ItemImageService(new ItemCatalog(), fixture.Directories, new UltimaReadGate());

        Assert.True(service.IsReady);
    }
}
