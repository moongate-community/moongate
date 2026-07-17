using Moongate.Http.Plugin.Services.Mobiles;
using Moongate.Tests.Support;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Http.Services.Mobiles;

public class BodyImageServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("mg-bodies-").FullName;

    public void Dispose()
        => Directory.Delete(_root, true);

    private BodyImageService Build(FakeAnimationCatalog catalog)
        => new(catalog, new DirectoriesConfig(_root, Array.Empty<string>()), new StubUltimaReadGate());

    [Fact]
    public async Task GetOrCreate_DecodableBody_WritesThePngOnceAndReusesIt()
    {
        var catalog = new FakeAnimationCatalog();
        catalog.Frames[(400, 0)] = (W: 20, H: 40, Cx: 10, Cy: 0);
        var service = Build(catalog);

        var first = await service.GetOrCreateAsync(400, 0);
        var again = await service.GetOrCreateAsync(400, 0);

        Assert.NotNull(first);
        Assert.True(File.Exists(first));
        Assert.Equal(first, again);
    }

    [Fact]
    public async Task GetOrCreate_HuedVariant_IsItsOwnFile()
    {
        var catalog = new FakeAnimationCatalog();
        catalog.Frames[(400, 0)] = (W: 20, H: 40, Cx: 10, Cy: 0);
        catalog.Frames[(400, 1153)] = (W: 20, H: 40, Cx: 10, Cy: 0);
        var service = Build(catalog);

        var plain = await service.GetOrCreateAsync(400, 0);
        var hued = await service.GetOrCreateAsync(400, 1153);

        Assert.NotEqual(plain, hued);
    }

    [Fact]
    public async Task GetOrCreate_UnknownBody_ReturnsNull()
    {
        var service = Build(new FakeAnimationCatalog());

        Assert.Null(await service.GetOrCreateAsync(999, 0));
    }
}
