using Moongate.Server.Services.Items;
using Moongate.UO.Data.Loot;

namespace Moongate.Tests.Server;

public class LootTemplateServiceTests
{
    [Fact]
    public void Register_ThenGetById_IsCaseInsensitive()
    {
        var service = new LootTemplateService();
        service.Register(new LootTemplate { Id = "creature.balron", Name = "Balron" });

        Assert.Equal(1, service.Count);
        Assert.Equal("Balron", service.GetById("CREATURE.BALRON")!.Name);
        Assert.Null(service.GetById("missing"));
    }

    [Fact]
    public void Register_WithCaseInsensitiveExistingId_ReplacesValue()
    {
        var service = new LootTemplateService();
        service.Register(new LootTemplate { Id = "creature.balron", Name = "Balron" });
        service.Register(new LootTemplate { Id = "CREATURE.BALRON", Name = "Updated Balron" });

        Assert.Equal(1, service.Count);
        Assert.Equal("Updated Balron", service.GetById("creature.balron")!.Name);
    }

    [Fact]
    public void All_IsOrderedById()
    {
        var service = new LootTemplateService();
        service.Register(new LootTemplate { Id = "z" });
        service.Register(new LootTemplate { Id = "A" });

        Assert.Equal(new[] { "A", "z" }, service.All.Select(template => template.Id));
    }
}
