using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ItemScriptServiceTests : IDisposable
{
    private readonly TemporaryDirectory _scripts = new();
    private readonly FakeScriptEngine _engine = new();
    private readonly ItemEntity _potion = new()
    {
        Id = new Serial(0x40000100), TemplateId = "potion", ItemId = 0x0F0E, Amount = 3
    };

    [Fact]
    public async Task StartAsync_LoadsTheItemScriptsInNameOrderAndSkipsABrokenOne()
    {
        _scripts.CreateFile("items/potion.lua");
        _scripts.CreateFile("items/chest.lua");
        _scripts.CreateFile("items/old/stale.lua");
        _scripts.CreateFile("mobiles/wander.lua");
        _engine.LoadFileThrows = new InvalidOperationException("items/chest.lua:1: unexpected symbol");

        await Create().StartAsync();

        Assert.Equal(["items/chest.lua", "items/potion.lua"], _engine.Loaded);
    }

    [Fact]
    public async Task Run_CallsTheFunctionOfTheTemplateScriptWithTheItemSerialFirst()
    {
        var service = Create(new ItemTemplate { Id = "potion", ScriptId = "potion" });
        await service.StartAsync();

        service.Run(_potion, "on_use", 2L);

        var call = Assert.Single(_engine.MemberCalls);
        Assert.Equal(("items/potion.lua", "potion", "on_use"), (call.Owner, call.Table, call.Function));
        Assert.Equal([0x40000100L, 2L], call.Args);
    }

    [Fact]
    public async Task Run_NoScriptOrUnknownTemplate_IsMissing()
    {
        var service = Create(new ItemTemplate { Id = "potion", ScriptId = "" });
        await service.StartAsync();

        Assert.Equal(ScriptResultKind.Missing, service.Run(_potion, "on_use").Kind);
        Assert.Equal(ScriptResultKind.Missing, service.Run(new ItemEntity { Id = new Serial(0x40000101), TemplateId = "gone" }, "on_use").Kind);
        Assert.Empty(_engine.MemberCalls);
    }

    [Fact]
    public async Task Run_BeforeStartOrAfterStop_IsMissing()
    {
        var service = Create(new ItemTemplate { Id = "potion", ScriptId = "potion" });

        Assert.Equal(ScriptResultKind.Missing, service.Run(_potion, "on_use").Kind);
        await service.StartAsync();
        await service.StopAsync();
        Assert.Equal(ScriptResultKind.Missing, service.Run(_potion, "on_use").Kind);
        Assert.Empty(_engine.MemberCalls);
    }

    public void Dispose()
    {
        _scripts.Dispose();
    }

    private ItemScriptService Create(params ItemTemplate[] templates)
    {
        return new(
            _engine,
            new ItemTemplateService(new StubDataLoaderService().With(templates)),
            new StubGameLoop(),
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
    }
}
