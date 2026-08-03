using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Services.Mobiles;

namespace Moongate.Tests.Server.Mobiles;

public class MobileTemplateServiceTests
{
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

    // The admin catalogue pages over this, and a page taken from an unordered dictionary can repeat
    // a row or drop one with nothing in the response admitting it. The item registry orders by id
    // for the same reason.
    [Fact]
    public void All_IsOrderedById()
    {
        IMobileTemplateService service = new MobileTemplateService();

        service.Register(new() { Id = "orc" });
        service.Register(new() { Id = "brigand" });
        service.Register(new() { Id = "Zombie" });

        Assert.Equal(["brigand", "orc", "Zombie"], service.All.Select(template => template.Id));
    }

    [Fact]
    public void All_IsEmptyBeforeAnythingIsRegistered()
        => Assert.Empty(((IMobileTemplateService)new MobileTemplateService()).All);

    // Registering the same id twice is a reload, not a duplicate: the loader re-registers on startup.
    [Fact]
    public void All_ReportsOneEntryPerId()
    {
        IMobileTemplateService service = new MobileTemplateService();

        service.Register(new() { Id = "orc", Name = "an orc" });
        service.Register(new() { Id = "orc", Name = "an orc chief" });

        Assert.Equal("an orc chief", Assert.Single(service.All).Name);
    }
}
