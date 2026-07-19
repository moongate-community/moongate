using Moongate.Http.Plugin.Services.Mobiles;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Mobiles;
using Moongate.Tests.Support;

namespace Moongate.Tests.Http.Services.Mobiles;

public sealed class MobileTemplateImageServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("mg-figures-").FullName;

    public void Dispose()
        => Directory.Delete(_root, true);

    [Fact]
    public async Task GetOrCreate_SeededTemplate_RendersDeterministicallyFromTheSpecLowEnd()
    {
        var catalog = new FakeAnimationCatalog();

        // hue(33:44) must resolve to 33 — only that pair decodes; a random pick would 404 the test.
        catalog.Frames[(400, 33)] = (W: 20, H: 40, Cx: 10, Cy: 0);
        var templates = new MobileTemplateService();
        templates.Register(
            new()
            {
                Id = "villager",
                Appearance = new() { Body = 400, SkinHue = "hue(33:44)" }
            }
        );
        var service = new MobileTemplateImageService(
            new MobileFigureRenderer(catalog, new ItemTemplateService()),
            templates,
            new(_root, Array.Empty<string>()),
            new StubUltimaReadGate()
        );

        var path = await service.GetOrCreateAsync("villager");

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task GetOrCreate_UnknownTemplate_ReturnsNull()
    {
        var service = new MobileTemplateImageService(
            new MobileFigureRenderer(new FakeAnimationCatalog(), new ItemTemplateService()),
            new MobileTemplateService(),
            new(_root, Array.Empty<string>()),
            new StubUltimaReadGate()
        );

        Assert.Null(await service.GetOrCreateAsync("nope"));
    }
}
