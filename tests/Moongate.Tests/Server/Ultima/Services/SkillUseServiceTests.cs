using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Jail;
using Moongate.Tests.TestSupport.Ultima.Skills;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class SkillUseServiceTests : IAsyncLifetime
{
    private readonly SettableClock _time = new();
    private readonly StubSkillScriptService _scripts = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly StubJailService _jail = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private SkillUseService _skills = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _skills = new(
            _fixture.Mobiles,
            _scripts,
            _speech,
            _time,
            _jail,
            TestLocalization.With((SkillUseService.NoSkillsInJailMessage, "Niente abilità in prigione."))
        );
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void Use_RunsTheScriptOfTheSkillForTheCharacter()
    {
        Assert.True(_skills.Use(_session, SkillType.Hiding));

        Assert.Equal([(SkillType.Hiding, _aria)], _scripts.Used);
        Assert.Empty(_speech.ToldClilocs);
    }

    [Theory]
    [InlineData(10.0)]
    [InlineData(10L)]
    [InlineData(10)]
    public void Use_ThenWaitsTheSecondsTheScriptReturned(object seconds)
    {
        _scripts.Result = ScriptResult.Completed([seconds]);
        _skills.Use(_session, SkillType.Hiding);

        _time.Advance(TimeSpan.FromSeconds(9.9));
        Assert.False(_skills.Use(_session, SkillType.Stealth));
        _time.Advance(TimeSpan.FromSeconds(0.1));
        Assert.True(_skills.Use(_session, SkillType.Stealth));

        Assert.Equal([SkillType.Hiding, SkillType.Stealth], _scripts.Used.Select(used => used.Skill));
        Assert.Equal([(_aria, SkillUseService.MustWaitCliloc, "")], _speech.ToldClilocs);
    }

    [Fact]
    public void Use_TooSoonAgainAndAgain_SaysToWaitOnceASecond()
    {
        _scripts.Result = ScriptResult.Completed([10.0]);
        _skills.Use(_session, SkillType.Hiding);

        // A macro in a loop: three tries at once, one a second later.
        _skills.Use(_session, SkillType.Hiding);
        _skills.Use(_session, SkillType.Hiding);
        _time.Advance(TimeSpan.FromSeconds(0.9));
        _skills.Use(_session, SkillType.Hiding);
        Assert.Single(_speech.ToldClilocs);

        _time.Advance(TimeSpan.FromSeconds(0.1));
        _skills.Use(_session, SkillType.Hiding);

        Assert.Equal(2, _speech.ToldClilocs.Count);
        Assert.Single(_scripts.Used);
    }

    [Fact]
    public void Use_AScriptThatReturnsNothing_WaitsOneSecond()
    {
        _skills.Use(_session, SkillType.Hiding);

        _time.Advance(TimeSpan.FromSeconds(0.9));
        Assert.False(_skills.Use(_session, SkillType.Hiding));
        _time.Advance(TimeSpan.FromSeconds(0.1));
        Assert.True(_skills.Use(_session, SkillType.Hiding));
    }

    [Theory]
    [InlineData("soon")]
    [InlineData(true)]
    [InlineData(double.NaN)]
    public void Use_AScriptThatReturnsNoNumber_WaitsOneSecond(object returned)
    {
        _scripts.Result = ScriptResult.Completed([returned]);
        _skills.Use(_session, SkillType.Hiding);

        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.True(_skills.Use(_session, SkillType.Hiding));
    }

    [Theory]
    [InlineData(-5.0, 0)]
    [InlineData(double.PositiveInfinity, 3600)]
    public void Use_KeepsTheWaitBetweenNoneAndAnHour(double returned, int expected)
    {
        _scripts.Result = ScriptResult.Completed([returned]);

        _skills.Use(_session, SkillType.Hiding);

        Assert.Equal(_time.GetUtcNow().AddSeconds(expected), _aria.NextSkillAt);
    }

    [Fact]
    public void Use_ASkillWithoutAScript_SaysItCannotBeUsed_AndAsksNoWait()
    {
        _scripts.Result = ScriptResult.Missing;

        Assert.False(_skills.Use(_session, SkillType.Magery));
        Assert.False(_skills.Use(_session, SkillType.Magery));

        Assert.Equal(
            [(_aria, SkillUseService.CannotUseCliloc, ""), (_aria, SkillUseService.CannotUseCliloc, "")],
            _speech.ToldClilocs
        );
        Assert.Null(_aria.NextSkillAt);
    }

    [Fact]
    public void Use_AScriptThatIsStillRunning_AsksTenSeconds_SoItIsNotStartedAgainMeanwhile()
    {
        // on_use called wait(): what it returns later is not read.
        _scripts.Result = ScriptResult.Suspended;

        Assert.True(_skills.Use(_session, SkillType.Hiding));

        Assert.Equal(_time.GetUtcNow().AddSeconds(10), _aria.NextSkillAt);
    }

    [Fact]
    public void Use_APrisoner_IsRefused_AndRunsNoScript()
    {
        _jail.SentenceList.Add(new() { Id = _aria.Id, Cell = 1 });

        Assert.False(_skills.Use(_session, SkillType.Hiding));

        Assert.Empty(_scripts.Used);
        Assert.Equal([(_aria, "Niente abilità in prigione.")], _speech.Told);
        Assert.Null(_aria.NextSkillAt);
    }

    [Fact]
    public async Task Use_AJailedGameMaster_StillUsesItsSkills()
    {
        _jail.SentenceList.Add(new() { Id = _aria.Id, Cell = 1 });
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));

        Assert.True(_skills.Use(_session, SkillType.Hiding));
    }

    [Fact]
    public void Use_WhileTheScriptOfASkillRuns_AnotherSkillMustWait()
    {
        var inner = true;
        _scripts.During = () =>
        {
            if (_scripts.Used.Count == 1)
            {
                inner = _skills.Use(_session, SkillType.Stealth);
            }
        };

        _skills.Use(_session, SkillType.Hiding);

        Assert.False(inner);
        Assert.Single(_scripts.Used);
    }

    [Fact]
    public async Task Use_ASessionWithoutACharacterInTheWorld_DoesNothing()
    {
        var lobby = await _fixture.AddAsync(3, false);

        Assert.False(_skills.Use(lobby, SkillType.Hiding));

        Assert.Empty(_scripts.Used);
        Assert.Empty(_speech.ToldClilocs);
    }
}
