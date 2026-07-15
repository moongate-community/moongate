using MoonSharp.Interpreter;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Scripting;
using Moongate.Server.Services.Items;
using Moongate.Tests.Support;
using Moongate.Ultima.Types;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Tests.Server;

public class ItemModuleTests
{
    [Fact]
    public void Create_PersistsAndReturnsSerial()
    {
        var (module, persistence) = Build();

        var serial = module.Create("dagger", 1, 0);

        Assert.NotNull(serial);
        Assert.NotNull(persistence.Store<ItemEntity>().GetById((Serial)serial!.Value));
    }

    [Fact]
    public void Create_UnknownTemplate_ReturnsNull()
    {
        var (module, _) = Build();
        Assert.Null(module.Create("nope", 1, 0));
    }

    [Fact]
    public void Create_AppliesAmountAndHue()
    {
        var (module, persistence) = Build();

        var serial = module.Create("dagger", 5, 1153);

        var item = persistence.Store<ItemEntity>().GetById((Serial)serial!.Value)!;
        Assert.Equal(5, item.Amount);
        Assert.Equal((ushort)1153, item.Hue.Value);
    }

    [Fact]
    public void Get_ReturnsFieldTable()
    {
        var (module, _) = Build();
        var serial = module.Create("dagger", 2, 0)!.Value;

        var table = module.Get(serial);

        Assert.NotNull(table);
        Assert.Equal(3921, table!["item_id"]);
        Assert.Equal("Dagger", table["name"]);
        Assert.Equal(2, table["amount"]);
    }

    [Fact]
    public void Get_UnknownSerial_ReturnsNull()
    {
        var (module, _) = Build();
        Assert.Null(module.Get(999999u));
    }

    [Fact]
    public void Set_MutatesAndPersists()
    {
        var (module, persistence) = Build();
        var serial = module.Create("dagger", 1, 0)!.Value;

        var fields = new Table(new Script());
        fields["amount"] = 7;
        fields["hue"] = 42;

        Assert.True(module.Set(serial, fields));

        var item = persistence.Store<ItemEntity>().GetById((Serial)serial)!;
        Assert.Equal(7, item.Amount);
        Assert.Equal((ushort)42, item.Hue.Value);
    }

    [Fact]
    public void Flip_CyclesGraphic()
    {
        var (module, _) = Build();
        var serial = module.Create("armoire", 1, 0)!.Value;

        Assert.True(module.Flip(serial));
        Assert.Equal(2643, module.Get(serial)!["item_id"]);
    }

    [Fact]
    public void Delete_RemovesItem()
    {
        var (module, persistence) = Build();
        var serial = module.Create("dagger", 1, 0)!.Value;

        Assert.True(module.Delete(serial));
        Assert.Null(persistence.Store<ItemEntity>().GetById((Serial)serial));
    }

    [Fact]
    public void Equip_And_Equipped()
    {
        var (module, persistence) = Build();
        var mobile = new MobileEntity { Name = "Bob" };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).GetAwaiter().GetResult();
        var serial = module.Create("dagger", 1, 0)!.Value;

        Assert.True(module.Equip(mobile.Id.Value, serial, "OneHanded"));
        Assert.Contains(serial, module.Equipped(mobile.Id.Value));
    }

    [Fact]
    public void Equip_UnknownLayer_ReturnsFalse()
    {
        var (module, persistence) = Build();
        var mobile = new MobileEntity { Name = "Bob" };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).GetAwaiter().GetResult();
        var serial = module.Create("dagger", 1, 0)!.Value;

        Assert.False(module.Equip(mobile.Id.Value, serial, "NotALayer"));
    }

    [Fact]
    public void Unequip_ReturnsSerial()
    {
        var (module, persistence) = Build();
        var mobile = new MobileEntity { Name = "Bob" };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).GetAwaiter().GetResult();
        var serial = module.Create("dagger", 1, 0)!.Value;
        module.Equip(mobile.Id.Value, serial, "OneHanded");

        Assert.Equal(serial, module.Unequip(mobile.Id.Value, "OneHanded"));
    }

    [Fact]
    public void Equip_AcceptsNumericLayerConstant()
    {
        var (module, persistence) = Build();
        var mobile = new MobileEntity { Name = "Bob" };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).GetAwaiter().GetResult();
        var serial = module.Create("dagger", 1, 0)!.Value;

        // Lua passes an exposed LayerType constant as a number.
        Assert.True(module.Equip(mobile.Id.Value, serial, (int)LayerType.OneHanded));
        Assert.Equal(serial, module.Unequip(mobile.Id.Value, (int)LayerType.OneHanded));
    }

    [Fact]
    public void Container_AddContentsRemove()
    {
        var (module, _) = Build();
        var container = module.Create("dagger", 1, 0)!.Value;
        var item = module.Create("dagger", 1, 0)!.Value;

        Assert.True(module.AddToContainer(container, item, 3, 4));
        Assert.Contains(item, module.Contents(container));

        Assert.True(module.RemoveFromContainer(container, item));
        Assert.DoesNotContain(item, module.Contents(container));
    }

    private static (ItemModule Module, FakePersistenceService Persistence) Build()
    {
        var persistence = new FakePersistenceService();
        var templates = new ItemTemplateService();
        templates.Register(new() { Id = "dagger", Name = "Dagger", Category = "Weapons", ItemId = 3921 });
        templates.Register(
            new()
            {
                Id = "armoire", Name = "Armoire", Category = "Containers", ItemId = 2639,
                FlippableItemIds = [2639, 2643]
            }
        );

        var factory = new ItemFactoryService(templates, new(1));
        var itemService = new ItemService(persistence);

        return (new ItemModule(factory, itemService, persistence, new StubLoopThread()), persistence);
    }
}
