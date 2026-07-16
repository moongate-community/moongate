using MoonSharp.Interpreter;
using Moongate.Server.Data.Events;
using SquidStd.Core.Data.Events;
using SquidStd.Scripting.Lua.Services;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.Scripting;

public class WorldReadyForwardingTests
{
    [Fact]
    public async Task WorldReadyEvent_ReachesLuaWorldReadyHandler()
    {
        using var bus = new EventBusService(new EventBusOptions());
        var script = new Script();
        var bridge = new LuaEventBridge();
        bridge.Attach(script);
        var forwarder = new LuaEventBusForwarder(bridge, bus);
        await forwarder.StartAsync();
        var callback = script.DoString("return function(e) fired = true end").Function;
        bridge.Register("world_ready", callback);

        await bus.PublishAsync(new WorldReadyEvent());

        Assert.True(script.Globals.Get("fired").Boolean);

        forwarder.Dispose();
    }
}
