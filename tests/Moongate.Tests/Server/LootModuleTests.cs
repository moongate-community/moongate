using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Scripting;
using Moongate.Server.Services.Items;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class LootModuleTests
{
    [Fact]
    public void Roll_PersistsItemsAndReturnsSerials()
    {
        var loot = new LootTemplateService();
        loot.Register(
            new()
            {
                Id = "t",
                Mode = LootTemplateModeType.Additive,
                Rolls = 1,
                Entries =
                [
                    new() { ItemTemplateId = "gold", Amount = 1, Chance = 1.0 },
                    new() { ItemTemplateId = "gold", Amount = 1, Chance = 1.0 }
                ]
            }
        );
        var (module, persistence) = Build(loot);

        var serials = module.Roll("t");

        Assert.Equal(2, serials.Count);
        Assert.All(
            serials,
            serial => Assert.Equal(3821, persistence.Store<ItemEntity>().GetById((Serial)serial)!.ItemId)
        );
    }

    [Fact]
    public void Roll_UnknownTable_ReturnsEmpty()
        => Assert.Empty(Build(new LootTemplateService()).Module.Roll("nope"));

    private static (LootModule Module, FakePersistenceService Persistence) Build(LootTemplateService loot)
    {
        var persistence = new FakePersistenceService();
        var itemTemplates = new ItemTemplateService();
        itemTemplates.Register(new() { Id = "gold", Name = "Gold", Category = "Currency", ItemId = 3821 });

        var itemFactory = new ItemFactoryService(itemTemplates, new Random(1));
        var lootService = new LootService(loot, itemFactory, new Random(1));
        var items = new ItemService(persistence);

        return (new LootModule(lootService, items, new StubLoopThread()), persistence);
    }
}
