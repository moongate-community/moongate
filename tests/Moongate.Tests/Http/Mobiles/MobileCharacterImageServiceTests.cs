using Moongate.Core.Extensions;
using Moongate.Core.Primitives;
using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Http.Plugin.Interfaces.Ultima;
using Moongate.Http.Plugin.Services.Mobiles;
using Moongate.Persistence.Entities;
using Moongate.Server.Services.Items;
using Moongate.Tests.Support;
using Moongate.Ultima.Imaging;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Http.Mobiles;

/// <summary>
/// The cache is content-addressed, so the behaviour worth pinning is that a change of clothes
/// produces a different file rather than replacing one — and that serving from cache moves the
/// file's write time, because the sweep reads it.
/// </summary>
public class MobileCharacterImageServiceTests
{
    private sealed class Fixture
    {
        private readonly FakePersistenceService _persistence = new();
        private readonly ItemService _items;

        public Fixture()
        {
            _items = new(_persistence);
            Renderer = new();

            var root = TemporaryDirectory.Create("mg-character-images-");

            Service = new(
                Renderer,
                Renderer,
                _persistence,
                _items,
                new(root, []),
                new ImmediateReadGate()
            );
        }

        public RecordingRenderer Renderer { get; }

        public MobileCharacterImageService Service { get; }

        public void Equip(MobileEntity mobile, int itemId, LayerType layer)
        {
            var item = new ItemEntity { ItemId = itemId };

            _items.Save(item);
            _items.Equip(mobile, item, layer);
        }

        public MobileEntity Mobile()
        {
            var mobile = new MobileEntity
            {
                Name = "Squid",
                Body = 400,
                SkinHue = new(1001),
                HairStyle = 0x203B,
                HairHue = new(1102),
                MapId = 1,
                Position = new(1, 1, 0)
            };

            _persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();

            return mobile;
        }
    }

    /// <summary>Counts renders and returns a 1x1 bitmap, so the tests are about caching, not pixels.</summary>
    private sealed class RecordingRenderer : IMobileFigureRenderer, IPaperdollRenderer
    {
        public int Renders { get; private set; }

        public UltimaBitmap? Render(MobileFigureRequest request)
        {
            Renders++;

            return new(1, 1);
        }

        public UltimaBitmap? Render(PaperdollRenderRequest request)
        {
            Renders++;

            return new(1, 1);
        }
    }

    private sealed class ImmediateReadGate : IUltimaReadGate
    {
        public Task<T> ReadAsync<T>(Func<T> read, CancellationToken cancellationToken = default)
            => Task.FromResult(read());
    }

    // The point of the whole design: dressing differently is a different file, and the old one is
    // left alone rather than invalidated.
    [Fact]
    public async Task GetFigure_AfterTheCharacterChangesClothes_IsADifferentFile()
    {
        var world = new Fixture();
        var mobile = world.Mobile();

        var before = await world.Service.GetFigureAsync(mobile.Id);

        world.Equip(mobile, 0x1410, LayerType.Helm);

        var after = await world.Service.GetFigureAsync(mobile.Id);

        Assert.NotEqual(before!.Hash, after!.Hash);
        Assert.NotEqual(before.Path, after.Path);
        Assert.True(File.Exists(before.Path), "the old file is left alone, not invalidated");
    }

    // Null rather than an exception: the route turns it into a 404, as the template routes do.
    [Fact]
    public async Task GetFigure_ForAnUnknownSerial_IsNull()
    {
        var world = new Fixture();

        Assert.Null(await world.Service.GetFigureAsync((Serial)0xDEAD));
    }

    // The sweep reads last-write time, so a cache hit has to move it or a picture in daily use
    // would look untouched and be swept.
    [Fact]
    public async Task GetFigure_OnACacheHit_TouchesTheFile()
    {
        var world = new Fixture();
        var mobile = world.Mobile();

        var image = await world.Service.GetFigureAsync(mobile.Id);
        var backdated = DateTime.UtcNow.AddDays(-30);

        File.SetLastWriteTimeUtc(image!.Path, backdated);

        await world.Service.GetFigureAsync(mobile.Id);

        Assert.True(File.GetLastWriteTimeUtc(image.Path) > backdated);
    }

    [Fact]
    public async Task GetFigure_RendersOnceAndReusesTheFile()
    {
        var world = new Fixture();
        var mobile = world.Mobile();

        var first = await world.Service.GetFigureAsync(mobile.Id);
        var second = await world.Service.GetFigureAsync(mobile.Id);

        Assert.Equal(first!.Path, second!.Path);
        Assert.Equal(1, world.Renderer.Renders);
    }

    [Fact]
    public async Task GetPaperdoll_ForAKnownCharacter_RendersAndCaches()
    {
        var world = new Fixture();
        var mobile = world.Mobile();

        var image = await world.Service.GetPaperdollAsync(mobile.Id, true);

        Assert.NotNull(image);
        Assert.True(File.Exists(image.Path));
    }

    // With and without the background are different pictures and must not share a file.
    [Fact]
    public async Task GetPaperdoll_WithAndWithoutBackground_AreDifferentFiles()
    {
        var world = new Fixture();
        var mobile = world.Mobile();

        var withBackground = await world.Service.GetPaperdollAsync(mobile.Id, true);
        var without = await world.Service.GetPaperdollAsync(mobile.Id, false);

        Assert.NotEqual(withBackground!.Path, without!.Path);
    }
}
