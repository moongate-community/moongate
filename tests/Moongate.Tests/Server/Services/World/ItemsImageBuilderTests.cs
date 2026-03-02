using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Interfaces.Art;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Items;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Tests.Server.Services.World;

public class ItemsImageBuilderTests
{
    [Test]
    public void Name_ShouldBeItemsImages()
    {
        using var context = new TestContext();
        var service = CreateService(context);

        Assert.That(service.Name, Is.EqualTo("items_images"));
    }

    [Test]
    public async Task GenerateAsync_ShouldWritePngForTemplatesWithAvailableArt()
    {
        using var context = new TestContext();
        context.ItemTemplateService.Upsert(
            new ItemTemplateDefinition
            {
                Id = "brick",
                Name = "Brick",
                Category = "test",
                ItemId = "0x1F9E"
            }
        );
        context.ItemTemplateService.Upsert(
            new ItemTemplateDefinition
            {
                Id = "invalid_art",
                Name = "Invalid Art",
                Category = "test",
                ItemId = "0xFFFF"
            }
        );

        context.ArtService.AddArt(0x1F9E, new Image<Rgba32>(2, 2));
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
    public async Task GenerateAsync_ShouldSkipInvalidItemIdWithoutThrowing()
    {
        using var context = new TestContext();
        context.ItemTemplateService.Upsert(
            new ItemTemplateDefinition
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

    private static ItemsImageBuilder CreateService(TestContext context)
        => new(context.ArtService, context.ItemTemplateService, context.DirectoriesConfig);

    private sealed class TestContext : IDisposable
    {
        private readonly string _root;

        public TestContext()
        {
            _root = Path.Combine(Path.GetTempPath(), "moongate-items-image-builder-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            DirectoriesConfig = new DirectoriesConfig(_root, Enum.GetNames<DirectoryType>());
            ItemTemplateService = new ItemTemplateService();
            ArtService = new FakeArtService();
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
        {
            _images[itemId] = image;
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

        public void Dispose()
        {
            foreach (var image in _images.Values)
            {
                image.Dispose();
            }
        }
    }
}
