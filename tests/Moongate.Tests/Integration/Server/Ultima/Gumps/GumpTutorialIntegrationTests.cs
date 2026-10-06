using DryIoc;
using Moongate.Core.Directories;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Modules;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Integration.Server.Ultima.Gumps;

/// <summary>
///     Runs the tutorial gumps and scripts shipped in <c>moongate_root</c> with the real Lua engine.
/// </summary>
public sealed class GumpTutorialIntegrationTests : IAsyncLifetime
{
    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingGumpService _gumps = new();
    private readonly List<ScriptErrorEvent> _errors = [];

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private LuaScriptEngineService _engine = null!;
    private GumpModule _module = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(7);

        foreach (var file in Directory.GetFiles(
                     Path.Combine(RepositoryRoot(), "moongate_root", "scripts", "gumps"),
                     "*.lua"
                 ))
        {
            _scripts.Write($"gumps/{Path.GetFileName(file)}", await File.ReadAllTextAsync(file));
        }

        _scripts.Write(
            "gumps/probe.lua",
            """
            probe = {}
            function probe.open_list(player) return gump.open(player, "tutorial_list") end
            function probe.open_twice(player) gump.open(player, "probe") gump.open(player, "probe") end
            function probe.on_close(player, args, reason) probe_closed = reason end
            function probe.closed() return probe_closed end
            """
        );
        var gumpTemplates = (await new GumpsLoader(
                    new DirectoriesConfig(Path.Combine(RepositoryRoot(), "moongate_root"), ["templates"])
                )
                .LoadDataAsync()).Entities
            .Append(
                new()
                {
                    Id = "probe", File = "probe.xml",
                    Root = System.Xml.Linq.XElement.Parse("""<gump id="probe"><text x="1" y="1">p</text></gump>""")
                }
            )
            .ToArray();
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path, MaxInstructionsPerResume = 20_000, MaxInstructionsPerChunk = 100_000,
            HookInterval = 100, WriteDefinitions = false
        };

        GumpScriptService? gumpScripts = null;
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IGumpService>(_gumps);
        _container.RegisterInstance<IGumpTemplateService>(
            new GumpTemplateService(_gumps, new StubDataLoaderService().With(gumpTemplates), _loop, _fixture.Sessions)
        );
        _container.RegisterDelegate<IGumpScriptService>(_ => gumpScripts!);
        _container.AddScriptModule<LogModule>();
        _container.AddScriptModule<GumpModule>();
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );

        _engine = new(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
        await _engine.StartAsync();
        gumpScripts = new GumpScriptService(_engine, _loop, options);
        await gumpScripts.StartAsync();
        _module = _container.Resolve<GumpModule>();
    }

    [Fact]
    public void TheNameStep_OpensTheGreetingWithTheName_AndDoneRunsItsScript()
    {
        Assert.True(_module.Open(7, "tutorial_name"));

        Answer(0, 1, texts: new() { [1] = "Aria" });

        var greeting = _gumps.Opened[1].Gump;
        Assert.Equal("tutorial_greeting", greeting.Id);
        Assert.Contains("Aria", greeting.Layout.Build().Strings[0]);
        Answer(1, 2);
        Assert.Empty(_errors);
    }

    [Fact]
    public void TheList_FillsItsSlotWithPagedRows_AndARowRunsItsFunction()
    {
        Assert.True(_module.Open(7, "tutorial_list"));

        var built = _gumps.Opened[0].Gump.Layout.Build();
        Assert.Contains("{ page 2 }", built.Layout);
        Assert.Contains("Britain", built.Strings);
        Assert.Contains("Yew", built.Strings);
        Answer(0, built.Buttons.Min());
        Assert.Empty(_errors);
    }

    [Fact]
    public void AScript_OpensAGumpWithASlot()
    {
        _loop.DeferTryPost = true;

        var result = _engine.CallMember("gumps/probe.lua", "probe", "open_list", 7L);
        _loop.RunDeferred();

        Assert.Equal([true], result.Values);
        Assert.Contains("Britain", Assert.Single(_gumps.Opened).Gump.Layout.Build().Strings);
        Assert.Empty(_errors);
    }

    [Fact]
    public void AScriptReplacingAGump_GetsItsOnClose()
    {
        _loop.DeferTryPost = true;

        _engine.CallMember("gumps/probe.lua", "probe", "open_twice", 7L);
        _loop.RunDeferred();

        Assert.Equal(["replaced"], _engine.CallMember("gumps/probe.lua", "probe", "closed").Values);
        Assert.Empty(_errors);
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }

    private void Answer(int gump, int button, Dictionary<int, string>? texts = null)
    {
        _gumps.Opened[gump]
            .Gump.OnResponse(
                _session,
                new GumpResponse { ButtonId = button, Switches = new HashSet<int>(), Texts = texts ?? [] }
            );
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }
}
