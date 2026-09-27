using Lua;
using Moongate.Scripting.Internal;

namespace Moongate.Tests.Scripting.Internal;

public sealed class ScriptEventSubscriptionsTests
{
    private static readonly LuaFunction Handler = new("handler", (context, _) => new(context.Return()));

    [Fact]
    public void Add_ReturnsDistinctHandles_AndSnapshotKeepsSubscriptionOrder()
    {
        var store = new ScriptEventSubscriptions();

        var first = store.Add("probe_fired", "a.lua", Handler);
        var second = store.Add("probe_fired", "b.lua", Handler);

        Assert.NotEqual(first, second);
        Assert.Equal([first, second], store.Snapshot("probe_fired").Select(s => s.Handle));
        Assert.Equal(["a.lua", "b.lua"], store.Snapshot("probe_fired").Select(s => s.Owner));
    }

    [Fact]
    public void Remove_KnownHandle_ReturnsTrueOnce()
    {
        var store = new ScriptEventSubscriptions();
        var handle = store.Add("probe_fired", "a.lua", Handler);

        Assert.True(store.Remove(handle));
        Assert.False(store.Remove(handle));
        Assert.False(store.HasSubscribers("probe_fired"));
    }

    [Fact]
    public void RemoveOwner_DropsOnlyThatFilesSubscriptions()
    {
        var store = new ScriptEventSubscriptions();
        store.Add("probe_fired", "a.lua", Handler);
        var kept = store.Add("probe_fired", "b.lua", Handler);

        store.RemoveOwner("a.lua");

        Assert.Equal([kept], store.Snapshot("probe_fired").Select(s => s.Handle));
    }

    [Fact]
    public void Snapshot_IsACopy_SoLaterChangesDoNotAlterIt()
    {
        var store = new ScriptEventSubscriptions();
        var handle = store.Add("probe_fired", "a.lua", Handler);
        var snapshot = store.Snapshot("probe_fired");

        store.Remove(handle);
        store.Add("probe_fired", "b.lua", Handler);

        Assert.Equal([handle], snapshot.Select(s => s.Handle));
    }

    [Fact]
    public void Snapshot_OnlyReturnsTheRequestedEvent()
    {
        var store = new ScriptEventSubscriptions();
        store.Add("probe_fired", "a.lua", Handler);
        var other = store.Add("other_event", "a.lua", Handler);

        Assert.Equal([other], store.Snapshot("other_event").Select(s => s.Handle));
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        var store = new ScriptEventSubscriptions();
        store.Add("probe_fired", "a.lua", Handler);

        store.Clear();

        Assert.False(store.HasSubscribers("probe_fired"));
        Assert.Empty(store.Snapshot("probe_fired"));
    }
}
