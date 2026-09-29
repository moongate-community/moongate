using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;
using Serilog;
using Serilog.Events;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class NpcScriptServiceTests : IDisposable
{
    private readonly TemporaryDirectory _scripts = new();
    private readonly FakeScriptEngine _engine = new();
    private readonly CapturingLogSink _log = new();
    private readonly StubGameLoop _loop = new();
    private readonly MobileEntity _orc = new()
    {
        Id = new Serial(0x100), Name = "an orc", TemplateId = "orc", Map = MapType.Trammel,
        Location = new Point3D(1600, 1600, 0)
    };

    [Fact]
    public async Task StartAsync_LoadsTheMobileScriptsInNameOrder()
    {
        _scripts.CreateFile("mobiles/wander.lua");
        _scripts.CreateFile("mobiles/guard.lua");
        _scripts.CreateFile("mobiles/readme.txt");
        _scripts.CreateFile("mobiles/old/stale.lua");
        _scripts.CreateFile("init.lua");

        await Create().StartAsync();

        Assert.Equal(["mobiles/guard.lua", "mobiles/wander.lua"], _engine.Loaded);
    }

    [Fact]
    public async Task StartAsync_WithoutAMobilesDirectory_LoadsNothing()
    {
        await Create().StartAsync();

        Assert.Empty(_engine.Loaded);
    }

    [Fact]
    public async Task StartAsync_AScriptThatDisappears_IsSkippedAndTheOthersLoad()
    {
        _scripts.CreateFile("mobiles/a.lua");
        _scripts.CreateFile("mobiles/b.lua");
        _engine.LoadFileThrows = new FileNotFoundException("gone");

        await Create().StartAsync();

        Assert.Equal(["mobiles/a.lua", "mobiles/b.lua"], _engine.Loaded);
    }

    [Fact]
    public async Task StartAsync_AScriptThatFailsToLoad_IsSkippedAndTheOthersLoad()
    {
        // The engine has already reported the error when LoadFile throws it.
        _scripts.CreateFile("mobiles/a.lua");
        _scripts.CreateFile("mobiles/b.lua");
        _engine.LoadFileThrows = new InvalidOperationException("mobiles/a.lua:1: unexpected symbol");

        await Create().StartAsync();

        Assert.Equal(["mobiles/a.lua", "mobiles/b.lua"], _engine.Loaded);
    }

    [Fact]
    public async Task Think_AfterStop_CallsNothing()
    {
        var service = await StartedAsync(new MobileTemplate { Id = "orc", ScriptId = "wander" });
        await service.StopAsync();

        service.Think(_orc);

        Assert.Empty(_engine.MemberCalls);
    }

    [Fact]
    public void Think_BeforeTheScriptsAreLoaded_CallsNothing()
    {
        // NPCs are loaded into the world before the script engine starts.
        Create(new MobileTemplate { Id = "orc", ScriptId = "wander" }).Think(_orc);

        Assert.Empty(_engine.MemberCalls);
    }

    [Fact]
    public async Task Queue_RunsTheFunctionOnTheNextLoopTurn()
    {
        var service = await StartedAsync(new MobileTemplate { Id = "orc", ScriptId = "wander" });
        _loop.DeferTryPost = true;

        service.Queue(_orc, "on_mobile_in_range", 2L);

        Assert.Empty(_engine.MemberCalls);
        _loop.RunDeferred();
        var call = Assert.Single(_engine.MemberCalls);
        Assert.Equal(("wander", "on_mobile_in_range"), (call.Table, call.Function));
        Assert.Equal([0x100L, 2L], call.Args);
    }

    [Fact]
    public async Task Queue_ForAnNpcWithoutAScript_PostsNothing()
    {
        var service = await StartedAsync(new MobileTemplate { Id = "orc" });
        var before = _loop.PostedWorkItems;

        service.Queue(_orc, "on_spawn");

        Assert.Equal(before, _loop.PostedWorkItems);
    }

    [Fact]
    public async Task Queue_WhenTheLoopRefuses_IsDroppedWithAWarning()
    {
        var service = await StartedAsync(new MobileTemplate { Id = "orc", ScriptId = "wander" });
        _loop.RefuseTryPost = true;

        service.Queue(_orc, "on_spawn");

        Assert.Empty(_engine.MemberCalls);
        Assert.Contains(_log.Events, e => e.Level == Serilog.Events.LogEventLevel.Warning);
    }

    [Fact]
    public async Task Think_CallsOnThinkOfTheTemplateScript()
    {
        (await StartedAsync(new MobileTemplate { Id = "orc", ScriptId = "wander" })).Think(_orc);

        var call = Assert.Single(_engine.MemberCalls);
        Assert.Equal(("mobiles/wander.lua", "wander", "on_think"), (call.Owner, call.Table, call.Function));
        Assert.Equal([0x100L], call.Args);
    }

    [Fact]
    public async Task Think_NoScriptOrUnknownTemplate_CallsNothing()
    {
        var service = await StartedAsync(new MobileTemplate { Id = "orc" });

        service.Think(_orc);
        service.Think(new MobileEntity { Id = new Serial(0x101), TemplateId = "gone" });

        Assert.Empty(_engine.MemberCalls);
    }

    [Fact]
    public async Task Think_ThatWaits_IsWarnedOncePerScript()
    {
        _engine.MemberResult = ScriptResult.Suspended;
        var service = await StartedAsync(new MobileTemplate { Id = "orc", ScriptId = "wander" });

        service.Think(_orc);
        service.Think(_orc);

        var warning = Assert.Single(_log.Events, e => e.Level == LogEventLevel.Warning);
        Assert.Contains("wander", warning.RenderMessage());
    }

    public void Dispose()
    {
        _scripts.Dispose();
    }

    private async Task<NpcScriptService> StartedAsync(params MobileTemplate[] templates)
    {
        var service = Create(templates);
        await service.StartAsync();

        return service;
    }

    private NpcScriptService Create(params MobileTemplate[] templates)
    {
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(_log).CreateLogger();

        return new(
            _engine,
            new MobileTemplateService(new StubDataLoaderService().With(templates)),
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path },
            logger
        );
    }
}
