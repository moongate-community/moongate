using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Interfaces.Art;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Services.Templates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Tests.Server.Services.World;

public class ItemsImageBuilderTests
{
    private sealed class TestContext : IDisposable
    {
        private readonly string _root;

        public TestContext()
        {
            _root = Path.Combine(Path.GetTempPath(), "moongate-items-image-builder-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            DirectoriesConfig = new(_root, Enum.GetNames<DirectoryType>());
            ItemTemplateService = new ItemTemplateService();
            ArtService = new();
        }

        public DirectoriesConfig DirectoriesConfig { get; }

        public IItemTemplateService ItemTemplateService { get; }

        public FakeArtService ArtService { get; }

        public void Dispose()
        {
            ArtService.Dispose();
            Directory.Delete(_root, true);
        }
    }

    private sealed class FakeArtService : IArtService, IDisposable
    {
        private readonly Dictionary<int, Image<Rgba32>> _images = [];

        public void AddArt(int itemId, Image<Rgba32> image)
            => _images[itemId] = image;

        public void Dispose()
        {
            foreach (var image in _images.Values)
            {
                image.Dispose();
            }
        }

        public Image<Rgba32>? GetArt(int itemId, bool clone = true)
        {
            if (!_images.TryGetValue(itemId, out var image))
            {
                return null;
            }

            return clone ? image.Clone() : image;
        }

        public bool IsValidArt(int itemId)
            => _images.ContainsKey(itemId);
    }

    [Test]
    public async Task GenerateAsync_ShouldSkipInvalidItemIdWithoutThrowing()
    {
        using var context = new TestContext();
        context.ItemTemplateService.Upsert(
            new()
            {
                Id = "invalid_id",
                Name = "Invalid",
                Category = "test",
                ItemId = "not_a_number"
            }
        );

        var service = CreateService(context);
        var logs = new List<string>();

        Assert.DoesNotThrowAsync(async () => await service.GenerateAsync(logs.Add));
        Assert.That(logs.Any(static x => x.Contains("invalid itemId")), Is.True);
    }

    [Test]
    public async Task GenerateAsync_ShouldWritePngForTemplatesWithAvailableArt()
    {
        using var context = new TestContext();
        context.ItemTemplateService.Upsert(
            new()
            {
                Id = "brick",
                Name = "Brick",
                Category = "test",
                ItemId = "0x1F9E"
            }
        );
        context.ItemTemplateService.Upsert(
            new()
            {
                Id = "invalid_art",
                Name = "Invalid Art",
                Category = "test",
                ItemId = "0xFFFF"
            }
        );

        context.ArtService.AddArt(0x1F9E, new(2, 2));
        var service = CreateService(context);
        var logs = new List<string>();

        await service.GenerateAsync(logs.Add);

        var imagesPath = context.DirectoriesConfig[DirectoryType.Images];
        var destinationPath = Path.Combine(imagesPath, "items");

        Assert.Multiple(
            () =>
            {
                Assert.That(File.Exists(Path.Combine(destinationPath, "brick_1F9E.png")), Is.True);
                Assert.That(File.Exists(Path.Combine(destinationPath, "invalid_art.png")), Is.False);
                Assert.That(logs.Any(static x => x.Contains("Items image export completed.")), Is.True);
            }
        );
    }

    [Test]
    public async Task GenerateAsync_ShouldCropTransparentBorderAndApplyFourPixelPadding()
    {
        using var context = new TestContext();
        context.ItemTemplateService.Upsert(
            new()
            {
                Id = "tiny_item",
                Name = "Tiny Item",
                Category = "test",
                ItemId = "0x1F9E"
            }
        );

        var source = new Image<Rgba32>(10, 10);
        source[4, 5] = new(255, 255, 255, 255);
        context.ArtService.AddArt(0x1F9E, source);

        var service = CreateService(context);

        await service.GenerateAsync();

        var outputPath = Path.Combine(
            context.DirectoriesConfig[DirectoryType.Images],
            "items",
            "tiny_item_1F9E.png"
        );

        using var generated = await Image.LoadAsync<Rgba32>(outputPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(generated.Width, Is.EqualTo(9));
                Assert.That(generated.Height, Is.EqualTo(9));
                Assert.That(generated[4, 4].A, Is.EqualTo(255));
                Assert.That(generated[0, 0].A, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public void Name_ShouldBeItemsImages()
    {
        using var context = new TestContext();
        var service = CreateService(context);

        Assert.That(service.Name, Is.EqualTo("items_images"));
    }

    private static ItemsImageBuilder CreateService(TestContext context)
        => new(context.ArtService, context.ItemTemplateService, context.DirectoriesConfig);
}
