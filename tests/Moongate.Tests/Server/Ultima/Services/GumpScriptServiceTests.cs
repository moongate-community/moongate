using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class GumpScriptServiceTests : IDisposable
{
    private readonly TemporaryDirectory _scripts = new();
    private readonly FakeScriptEngine _engine = new();

    [Fact]
    public async Task StartAsync_LoadsTheGumpScripts()
    {
        _scripts.CreateFile("gumps/release_pet.lua");
        _scripts.CreateFile("items/potion.lua");

        await Create().StartAsync();

        Assert.Equal(["gumps/release_pet.lua"], _engine.Loaded);
    }

    [Fact]
    public async Task Call_CallsTheTableNamedAfterTheGump()
    {
        var service = Create();
        await service.StartAsync();

        service.Call("release_pet", "on_close", 7L, "player");

        var call = Assert.Single(_engine.MemberCalls);
        Assert.Equal(("gumps/release_pet.lua", "release_pet", "on_close"), (call.Owner, call.Table, call.Function));
        Assert.Equal([7L, "player"], call.Args);
    }

    [Fact]
    public async Task Call_BeforeTheStartOrAfterTheStop_IsMissing()
    {
        var service = Create();

        Assert.Equal(ScriptResultKind.Missing, service.Call("a", "on_close").Kind);
        await service.StartAsync();
        await service.StopAsync();
        Assert.Equal(ScriptResultKind.Missing, service.Call("a", "on_close").Kind);
        Assert.Empty(_engine.MemberCalls);
    }

    private GumpScriptService Create()
    {
        return new(_engine, new StubGameLoop(), new ScriptEngineOptions { ScriptsDirectory = _scripts.Path });
    }

    public void Dispose()
    {
        _scripts.Dispose();
    }
}
