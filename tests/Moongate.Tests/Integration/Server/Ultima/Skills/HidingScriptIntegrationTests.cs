using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Randomness;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Skills;

/// <summary>
///     The shipped <c>scripts/skills/hiding.lua</c>, with the real Lua engine, skill check and skill use.
/// </summary>
public sealed class HidingScriptIntegrationTests : IAsyncLifetime
{
    private const int Hidden = 501240;
    private const int Failed = 501241;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingMobileStateService _state = new();
    private readonly ScriptedRandom _random = new();
    private readonly SettableClock _time = new();

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private SkillUseService _use = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.AccountId = new Serial(0x42);
        _aria.Skills = [new MobileSkill { Skill = SkillType.Hiding, Base = 500 }];
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<IMobileStateService>(_state);
        _container.RegisterInstance<ITeleportService>(
            new TeleportService(
                _fixture.Mobiles,
                new RecordingWorldViewService(),
                _fixture.Sessions,
                _fixture.Sender,
                _fixture.Sectors,
                new StubBankService()
            )
        );
        var data = new StubDataLoaderService().With(new SkillContent { Id = SkillType.Hiding, GainFactor = 1.0, Delay = 10 });
        _container.RegisterInstance<ISkillService>(
            new SkillService(
                _state,
                data,
                new SkillsConfig(),
                _random
            )
        );
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<SkillModule>();
        _container.RegisterDelegate<IScriptEngine>(_ => _engine);
        _container.Resolve<IMoongateEventBus>()
                  .Subscribe<ScriptErrorEvent>(
                      (evt, _) =>
                      {
                          _errors.Add(evt);

                          return Task.CompletedTask;
                      }
                  );
        _scripts.Write("skills/hiding.lua", File.ReadAllText(ShippedScript("skills/hiding.lua")));

        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path,
            MaxInstructionsPerResume = 20_000,
            MaxInstructionsPerChunk = 100_000,
            HookInterval = 100
        };
        _engine = new(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
        await _engine.StartAsync();
        var skillScripts = new SkillScriptService(_engine, _loop, options);
        await skillScripts.StartAsync();
        _use = new(_fixture.Mobiles, skillScripts, _speech, _time, data);
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void Hiding_ATryThatPasses_HidesThePlayerOutOfWarMode_AndSaysSo()
    {
        _aria.WarMode = true;
        // 50 points between 0 and 100: half the tries pass.
        _random.Doubles(0.5);

        Assert.True(_use.Use(_session, SkillType.Hiding));

        Assert.Empty(_errors);
        Assert.True(_aria.Hidden);
        Assert.False(_aria.WarMode);
        Assert.Equal([Hidden], Told());
    }

    [Fact]
    public void Hiding_ATryThatFails_ShowsThePlayer_AndSaysSo()
    {
        _aria.Hidden = true;
        _random.Doubles(0.51);

        Assert.True(_use.Use(_session, SkillType.Hiding));

        Assert.Empty(_errors);
        Assert.False(_aria.Hidden);
        Assert.Equal([Failed], Told());
    }

    [Fact]
    public void Hiding_AsksTheDelayOfTheSkillsFileBeforeAnotherSkill()
    {
        _use.Use(_session, SkillType.Hiding);

        _time.Advance(TimeSpan.FromSeconds(9.9));
        Assert.False(_use.Use(_session, SkillType.Hiding));
        _time.Advance(TimeSpan.FromSeconds(0.1));
        Assert.True(_use.Use(_session, SkillType.Hiding));

        Assert.Empty(_errors);
        Assert.Equal(SkillUseService.MustWaitCliloc, Told()[1]);
    }

    [Fact]
    public void Hiding_MayTeachTheSkill()
    {
        // A pass, then the roll that learns, then one tenth... as many as the integer asked says, plus one.
        _random.Doubles(0.1, 0.0).Integers(0);

        _use.Use(_session, SkillType.Hiding);

        Assert.Empty(_errors);
        Assert.Equal([(_aria, SkillType.Hiding, 501, (int?)null)], _state.SkillsSet);
    }

    [Fact]
    public void ASkillWithoutAScript_CannotBeUsed()
    {
        Assert.False(_use.Use(_session, SkillType.Magery));

        Assert.Empty(_errors);
        Assert.Equal([SkillUseService.CannotUseCliloc], Told());
    }

    private List<int> Told()
    {
        return _speech.ToldClilocs.Select(told => told.Cliloc).ToList();
    }

    private static string ShippedScript(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "moongate_root", "scripts", relativePath);
    }
}
