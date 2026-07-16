using Moongate.Server.Services.Mobiles;
using Moongate.UO.Data.Mobiles.Templates;

namespace Moongate.Tests.Server.Mobiles;

public class MobileTemplateServiceTests
{
    [Fact]
    public void Register_And_GetById_IsCaseInsensitive()
    {
        var service = new MobileTemplateService();
        service.Register(new() { Id = "Orc", Name = "An Orc" });

        Assert.Equal("An Orc", service.GetById("orc")!.Name);
        Assert.Equal(1, service.Count);
    }

    [Fact]
    public void Register_ReplacesById()
    {
        var service = new MobileTemplateService();
        service.Register(new() { Id = "orc", Name = "First" });
        service.Register(new() { Id = "orc", Name = "Second" });

        Assert.Equal(1, service.Count);
        Assert.Equal("Second", service.GetById("orc")!.Name);
    }

    [Fact]
    public void GetById_Unknown_ReturnsNull()
        => Assert.Null(new MobileTemplateService().GetById("nope"));

    [Fact]
    public void GetByTag_And_GetByCategory()
    {
        var service = new MobileTemplateService();
        service.Register(new() { Id = "guard", Category = "npc", Tags = ["town", "guard"] });
        service.Register(new() { Id = "skeleton", Category = "undead", Tags = ["undead"] });

        Assert.Equal("guard", Assert.Single(service.GetByTag("Town")).Id);
        Assert.Equal("skeleton", Assert.Single(service.GetByCategory("Undead")).Id);
    }
}
