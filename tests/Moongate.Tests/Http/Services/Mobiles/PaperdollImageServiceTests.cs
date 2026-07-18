using Moongate.Http.Plugin.Services.Mobiles;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Mobiles;
using Moongate.Tests.Support;
using Moongate.UO.Data.Mobiles.Templates;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Http.Services.Mobiles;

public sealed class PaperdollImageServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("mg-paperdoll-").FullName;

    public void Dispose()
        => Directory.Delete(_root, true);

    private PaperdollImageService Build(FakeGumpCatalog gumps, FakeAnimationCatalog anim, MobileTemplateService templates)
        => new(
            new PaperdollRenderer(gumps, anim, new ItemTemplateService()),
            templates,
            new DirectoriesConfig(_root, Array.Empty<string>()),
            new StubUltimaReadGate()
        );

    [Fact]
    public async Task GetOrCreate_MaleTemplate_WritesThePngOnceAndReusesIt()
    {
        var gumps = new FakeGumpCatalog();
        gumps.Gumps[(0x000C, 0)] = (W: 40, H: 100);
        var templates = new MobileTemplateService();
        templates.Register(new MobileTemplate { Id = "villager", Gender = MobileTemplateGenderType.Male });
        var service = Build(gumps, new FakeAnimationCatalog(), templates);

        var first = await service.GetOrCreateAsync("villager", true);
        var again = await service.GetOrCreateAsync("villager", true);

        Assert.NotNull(first);
        Assert.True(File.Exists(first));
        Assert.Equal(first, again);
    }

    [Fact]
    public async Task GetOrCreate_WithAndWithoutBackground_AreDifferentFiles()
    {
        var gumps = new FakeGumpCatalog();
        gumps.Gumps[(0x000C, 0)] = (W: 40, H: 100);
        gumps.Gumps[(0x07D0, 0)] = (W: 80, H: 120);
        var templates = new MobileTemplateService();
        templates.Register(new MobileTemplate { Id = "villager", Gender = MobileTemplateGenderType.Male });
        var service = Build(gumps, new FakeAnimationCatalog(), templates);

        var withBackground = await service.GetOrCreateAsync("villager", true);
        var without = await service.GetOrCreateAsync("villager", false);

        Assert.NotEqual(withBackground, without);
    }

    [Fact]
    public async Task GetOrCreate_FemaleTemplate_UsesTheFemaleBodyGump()
    {
        var gumps = new FakeGumpCatalog();
        gumps.Gumps[(0x000D, 0)] = (W: 40, H: 100); // only the female gump exists
        var templates = new MobileTemplateService();
        templates.Register(new MobileTemplate { Id = "priestess", Gender = MobileTemplateGenderType.Female });
        var service = Build(gumps, new FakeAnimationCatalog(), templates);

        Assert.NotNull(await service.GetOrCreateAsync("priestess", false));
    }

    [Fact]
    public async Task GetOrCreate_RandomGenderTemplate_ResolvesDeterministicallyToMale()
    {
        var gumps = new FakeGumpCatalog();
        gumps.Gumps[(0x000C, 0)] = (W: 40, H: 100); // only the male gump exists
        var templates = new MobileTemplateService();
        templates.Register(new MobileTemplate { Id = "wanderer", Gender = MobileTemplateGenderType.Random });
        var service = Build(gumps, new FakeAnimationCatalog(), templates);

        // A random pick would flip-flop between requests and 404 half the time; the cache key must be
        // stable, so Random resolves to a fixed choice — Male, the same "pick the low/first option"
        // philosophy LowestHue applies to hue ranges.
        Assert.NotNull(await service.GetOrCreateAsync("wanderer", false));
    }

    [Fact]
    public async Task GetOrCreate_UnknownTemplate_ReturnsNull()
    {
        var service = Build(new FakeGumpCatalog(), new FakeAnimationCatalog(), new MobileTemplateService());

        Assert.Null(await service.GetOrCreateAsync("nope", true));
    }
}
