using Moongate.Core.Extensions;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Scripting.Refs;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Mobiles;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using MoonSharp.Interpreter;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.Scripting.Refs;

/// <summary>
/// The handle holds a serial and never an entity, so every call re-reads and a target that died
/// between two calls is an ordinary false rather than a stale write.
/// </summary>
public class MobileRefFactoryTests
{
    [Fact]
    public void Create_CarriesExactlyTheThreeMethods()
    {
        var (factory, _, _) = Build(out var mobile);

        var table = factory.Create(mobile.Id).Table;

        // ClrFunction, not Function: MoonSharp marshals a Func<> into a callback rather than compiling
        // Lua. Both are callable from a script; only the tag differs.
        Assert.Equal(DataType.ClrFunction, table.Get("say").Type);
        Assert.Equal(DataType.ClrFunction, table.Get("teleport").Type);
        Assert.Equal(DataType.ClrFunction, table.Get("equip").Type);
        Assert.Equal(3, table.Keys.Count());
    }

    [Fact]
    public void Create_UnknownSerial_IsNil()
    {
        var (factory, _, _) = Build(out _);

        Assert.Equal(DataType.Nil, factory.Create((Serial)0xDEAD).Type);
    }

    // The layer arrives from Lua as a string or a number; anything else is not a layer.
    [Fact]
    public void Equip_LayerThatResolvesToNothing_IsFalse()
    {
        var (factory, script, _) = Build(out var mobile);

        var equipped = Call(
            script,
            factory.Create(mobile.Id),
            "equip",
            DynValue.NewNumber(1),
            DynValue.NewString("NotALayer")
        );

        Assert.False(equipped.Boolean);
    }

    // The reason the handle holds a serial: the mobile can die between ref and the call.
    [Fact]
    public void Say_AfterTheMobileIsGone_IsFalseAndSaysNothing()
    {
        var (factory, script, chat) = Build(out var mobile, out var persistence);

        var handle = factory.Create(mobile.Id);
        persistence.Store<MobileEntity>().RemoveAsync(mobile.Id).WaitSync();

        Assert.False(Call(script, handle, "say", DynValue.NewString("anyone there?")).Boolean);
        Assert.Empty(chat.Messages);
    }

    [Fact]
    public void Say_ReachesTheChatService()
    {
        var (factory, script, chat) = Build(out var mobile);

        var said = Call(script, factory.Create(mobile.Id), "say", DynValue.NewString("Welcome back."));

        Assert.True(said.Boolean);
        Assert.Single(chat.Messages);
    }

    [Fact]
    public void Teleport_MovesTheMobile()
    {
        var (factory, script, _) = Build(out var mobile, out var persistence);

        var moved = Call(
            script,
            factory.Create(mobile.Id),
            "teleport",
            DynValue.NewNumber(10),
            DynValue.NewNumber(20),
            DynValue.NewNumber(5)
        );

        Assert.True(moved.Boolean);
        Assert.Equal(new(10, 20, 5), persistence.Store<MobileEntity>().GetById(mobile.Id)!.Position);
    }

    private static (MobileRefFactory Factory, Script Script, RecordingChatService Chat) Build(out MobileEntity mobile)
        => Build(out mobile, out _);

    private static (MobileRefFactory Factory, Script Script, RecordingChatService Chat) Build(
        out MobileEntity mobile,
        out FakePersistenceService persistence
    )
    {
        var script = new Script();
        var store = new FakePersistenceService();
        var events = new EventBusService();
        var chat = new RecordingChatService();
        var spatial = new SpatialIndexService(store, new StubLoopAffinity(), events);

        mobile = new() { Name = "Squid", MapId = 1, Position = new(1, 1, 0) };
        store.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();

        persistence = store;

        return (
                   new(script, store, chat, new MobileService(store, spatial, events), new ItemService(store)),
                   script,
                   chat
               );
    }

    private static DynValue Call(Script script, DynValue handle, string method, params DynValue[] arguments)
        => script.Call(handle.Table.Get(method), arguments);
}
