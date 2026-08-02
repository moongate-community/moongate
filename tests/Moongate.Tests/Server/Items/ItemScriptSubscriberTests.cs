using Moongate.Core.Extensions;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Types.Items;
using Moongate.Server.Services.Items;
using Moongate.Server.Subscribers;
using Moongate.Tests.Support;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Items;

public class ItemScriptSubscriberTests
{
    [Fact]
    public async Task OnItemDoubleClick_InvokesTheDoubleClickHook()
    {
        var (subscriber, runtime, item, _) = Build();

        await subscriber.OnItemDoubleClick(new(1, item.Id), CancellationToken.None);

        var call = Assert.Single(runtime.Calls);
        Assert.Equal(ItemScriptHookType.DoubleClick, call.Hook);
        Assert.Equal(item.Id, call.Context.Item.Id);
    }

    [Fact]
    public async Task OnItemDoubleClick_UnknownItem_InvokesNothing()
    {
        var (subscriber, runtime, _, _) = Build();

        await subscriber.OnItemDoubleClick(new(1, (Serial)0xDEAD), CancellationToken.None);

        Assert.Empty(runtime.Calls);
    }

    [Fact]
    public async Task OnItemDropped_CarriesTheContainer()
    {
        var (subscriber, runtime, item, mobile) = Build();

        await subscriber.OnItemDropped(new(item.Id, mobile.Id, (Serial)99), CancellationToken.None);

        var call = Assert.Single(runtime.Calls);
        Assert.Equal(ItemScriptHookType.Dropped, call.Hook);
        Assert.Equal((Serial)99, call.Context.ContainerId);
    }

    [Fact]
    public async Task OnItemEquipped_CarriesTheLayer()
    {
        var (subscriber, runtime, item, mobile) = Build();

        await subscriber.OnItemEquipped(new(item.Id, mobile.Id, LayerType.Shirt), CancellationToken.None);

        var call = Assert.Single(runtime.Calls);
        Assert.Equal(ItemScriptHookType.Equipped, call.Hook);
        Assert.Equal(LayerType.Shirt, call.Context.Layer);
        Assert.Equal(mobile.Id, call.Context.Actor?.Id);
    }

    private static (ItemScriptSubscriber Subscriber, RecordingItemScriptRuntime Runtime, ItemEntity Item,
        MobileEntity Mobile) Build()
    {
        var persistence = new FakePersistenceService();
        var items = new ItemService(persistence);

        var item = new ItemEntity { TemplateId = "torch", ScriptId = "magic_torch", Name = "Torch" };
        items.Create(item);

        var mobile = new MobileEntity { MapId = 1, Position = new(100, 100, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();

        var runtime = new RecordingItemScriptRuntime();

        return (new(items, persistence, runtime, new StubSessionManager()), runtime, item, mobile);
    }
}
