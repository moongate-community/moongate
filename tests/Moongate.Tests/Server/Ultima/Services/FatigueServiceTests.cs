using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Internal.Movement;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Weight;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class FatigueServiceTests : IAsyncLifetime
{
    private readonly StubWeightService _weight = new() { CarriedStones = 50, MaximumStones = 100 };
    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly RecordingSpeechService _speech = new();
    private readonly RegenerationConfig _config = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private FatigueService _fatigue = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(MovementSessionKeys.State, new MovementState()));
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.AccountId = new Serial(0x42);
        _aria.Stamina = 100;
        _aria.StaminaMax = 100;
        _fatigue = new(_weight, _state, _speech, _config);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task Walking_WithALightLoad_CostsNothing_AndIsNeverRefused()
    {
        await StepsAsync(100, false);
        Assert.Equal(100, _aria.Stamina);

        _aria.Stamina = 0;
        Assert.True(await CanStepAsync(false));
        Assert.Empty(_speech.ToldClilocs);
    }

    [Fact]
    public async Task Running_CostsAPointEverySixteenSteps()
    {
        await StepsAsync(15, true);
        Assert.Equal(100, _aria.Stamina);

        await StepsAsync(1, true);
        Assert.Equal(99, _aria.Stamina);

        await StepsAsync(16, true);
        Assert.Equal(98, _aria.Stamina);
    }

    [Fact]
    public async Task Running_BelowATenthOfTheStamina_CostsAPointAStep_AndAtNoneIsRefused()
    {
        _aria.Stamina = 3;

        await StepsAsync(2, true);
        Assert.Equal(1, _aria.Stamina);

        await StepsAsync(5, true);
        Assert.Equal(0, _aria.Stamina);
        Assert.False(await CanStepAsync(true));
        Assert.Equal((_aria, FatigueService.FatiguedMessage, ""), Assert.Single(_speech.ToldClilocs));
        // Walking still works.
        Assert.True(await CanStepAsync(false));
    }

    [Theory,
     // 60 stones over: 5 and 60 / 25.
     InlineData(160, false, 7),
     InlineData(160, true, 14),
     // A stone over is already 5 a step.
     InlineData(101, false, 5),
     // At the maximum nothing.
     InlineData(100, false, 0)]
    public async Task Overloaded_EveryStepCosts_FiveAndOneMoreEveryTwentyFiveStonesOver_TwiceRunning(
        int carried,
        bool running,
        int loss
    )
    {
        _weight.CarriedStones = carried;

        await StepsAsync(1, running);

        Assert.Equal(100 - loss, _aria.Stamina);
    }

    [Fact]
    public async Task Overloaded_WithNoStaminaLeft_NoStepIsAllowed_AndThePlayerIsToldWhy()
    {
        _weight.CarriedStones = 160;
        _aria.Stamina = 6;

        await StepsAsync(1, false);
        Assert.Equal(0, _aria.Stamina);

        Assert.False(await CanStepAsync(false));
        Assert.Equal(FatigueService.OverloadedMessage, Assert.Single(_speech.ToldClilocs).Cliloc);
    }

    [Fact]
    public async Task Overloaded_AStepThatWouldTakeTheLastStamina_IsRefused_SoNoPointBuysATile()
    {
        // 100 stones over: 9 a step walking.
        _weight.CarriedStones = 200;
        _aria.Stamina = 9;

        Assert.False(await CanStepAsync(false));
        Assert.Equal(FatigueService.OverloadedMessage, Assert.Single(_speech.ToldClilocs).Cliloc);
        Assert.Equal(9, _aria.Stamina);

        _aria.Stamina = 10;
        Assert.True(await CanStepAsync(false));
        // Running costs twice as much.
        Assert.False(await CanStepAsync(true));
    }

    [Fact]
    public async Task Running_WithTheLastPointOfStamina_IsRefused()
    {
        _aria.Stamina = 1;

        Assert.False(await CanStepAsync(true));
        Assert.Equal(FatigueService.FatiguedMessage, Assert.Single(_speech.ToldClilocs).Cliloc);
        Assert.True(await CanStepAsync(false));
    }

    [Fact]
    public async Task TheStaff_AnNpc_AndAShardWithFatigueOff_PayNothing()
    {
        _weight.CarriedStones = 500;
        _aria.Stamina = 0;

        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));
        Assert.True(await CanStepAsync(true));
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.Regular));

        _aria.AccountId = null;
        Assert.True(await CanStepAsync(true));
        _aria.AccountId = new Serial(0x42);

        // A ghost has no stamina and gets none back: it still walks and runs.
        _aria.Body = 0x0192;
        _aria.Stamina = 0;
        Assert.True(await CanStepAsync(true));
        _aria.Body = 0x0190;

        _config.FatigueEnabled = false;
        _aria.Stamina = 50;
        Assert.True(await CanStepAsync(true));
        await StepsAsync(3, true);
        Assert.Equal(50, _aria.Stamina);
        Assert.Empty(_speech.ToldClilocs);
    }

    [Fact]
    public async Task LoadChanged_ShowsThePlayerItsWeight_AndWarnsWhenItCarriesMoreThanItMay()
    {
        await _fixture.Network.ExecuteOnLoopAsync(() => _fatigue.LoadChanged(_session, _aria, true));

        Assert.Equal((_session, _aria), Assert.Single(_state.Statuses));
        Assert.Empty(_speech.Told);

        _weight.CarriedStones = 130;
        await _fixture.Network.ExecuteOnLoopAsync(() => _fatigue.LoadChanged(_session, _aria, true));
        Assert.Equal((_aria, "You are overloaded: you carry 130 stones of 100."), Assert.Single(_speech.Told));

        // Lifting does not warn, and the staff is never told.
        await _fixture.Network.ExecuteOnLoopAsync(() => _fatigue.LoadChanged(_session, _aria, false));
        await _fixture.Network.ExecuteOnLoopAsync(() => _session.Set(SessionKeys.AccountType, AccountType.GameMaster));
        await _fixture.Network.ExecuteOnLoopAsync(() => _fatigue.LoadChanged(_session, _aria, true));

        Assert.Single(_speech.Told);
        Assert.Equal(4, _state.Statuses.Count);
    }

    private Task StepsAsync(int steps, bool running)
    {
        return _fixture.Network.ExecuteOnLoopAsync(() =>
            {
                for (var step = 0; step < steps; step++)
                {
                    _fatigue.Stepped(_session, _aria, running);
                }
            }
        );
    }

    private async Task<bool> CanStepAsync(bool running)
    {
        var allowed = false;
        await _fixture.Network.ExecuteOnLoopAsync(() => allowed = _fatigue.CanStep(_session, _aria, running));

        return allowed;
    }
}
