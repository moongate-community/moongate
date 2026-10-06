using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class MobileModulePathfindTests : IAsyncLifetime
{
    private BroadcastFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        _fixture.Mobiles.EnterWorld(new() { Id = new(0x100), Name = "a cat", Map = Moongate.Ultima.Types.MapType.Trammel });
    }

    // The client of that player walks there by itself, as after a double right click on the ground.
    [Fact]
    public void PathfindTo_TellsThePlayersClientWhereToWalk()
    {
        var result = Run("return mobile.pathfind_to(2, 1500, 1600, -20)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal(new Point3D(1500, 1600, -20), Assert.Single(_fixture.Sender.Sent.OfType<PathfindPacket>()).Destination);
    }

    [Theory]
    // An NPC has no client; nobody has that serial; a spot no map holds.
    [InlineData("mobile.pathfind_to(0x100, 1500, 1600, 0)")]
    [InlineData("mobile.pathfind_to(999, 1500, 1600, 0)")]
    [InlineData("mobile.pathfind_to(2, -1, 1600, 0)")]
    [InlineData("mobile.pathfind_to(2, 1500, 70000, 0)")]
    [InlineData("mobile.pathfind_to(2, 1500, 1600, 200)")]
    public void PathfindTo_WithNobodyToTell_OrNowhereToGo_SendsNothing(string call)
    {
        var result = Run("return " + call);

        Assert.False(result[0].Read<bool>());
        Assert.Empty(_fixture.Sender.Sent.OfType<PathfindPacket>());
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var module = new MobileModule(
            _fixture.Mobiles,
            new RecordingTeleportService(),
            new RecordingSpeechService(),
            sessions: _fixture.Sessions,
            sender: _fixture.Sender
        );
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, module);

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
