using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class StatBonusServiceTests : IAsyncLifetime
{
    private readonly RecordingTimerService _timers = new();
    private readonly RecordingMobileStateService _state = new() { Apply = true };

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private StatBonusService _bonuses = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.AccountId = new Serial(0x42);
        (_aria.Strength, _aria.HitsMax, _aria.Hits, _aria.Dexterity, _aria.StaminaMax, _aria.Stamina) = (50, 50, 50, 40, 40, 40);
        _bonuses = new(_state, _fixture.Sessions, _fixture.Sender, _timers, _fixture.Mobiles);
    }

    [Fact]
    public void ABonus_RaisesTheStat_ShowsTheStatus_AndASecondIsRefused()
    {
        Assert.True(_bonuses.TryAddBonus(_aria, StatBonusType.Strength, 10, TimeSpan.FromMinutes(2)));

        Assert.Equal((10, 60), (_aria.StrengthBonus, _aria.EffectiveHitsMax));
        Assert.Equal(10, _bonuses.Bonus(_aria, StatBonusType.Strength));
        Assert.Single(_state.Statuses);
        Assert.Equal(TimeSpan.FromMinutes(2), Assert.Single(_timers.Timers).Interval);

        Assert.False(_bonuses.TryAddBonus(_aria, StatBonusType.Strength, 20, TimeSpan.FromMinutes(2)));
        Assert.True(_bonuses.TryAddBonus(_aria, StatBonusType.Dexterity, 20, TimeSpan.FromMinutes(2)));
        Assert.Equal(20, _aria.DexterityBonus);
    }

    [Theory]
    [InlineData(0, 120)]
    [InlineData(10, 0)]
    public void ABonusOfNothing_OrForNoTime_IsRefused(int amount, int seconds)
    {
        Assert.False(_bonuses.TryAddBonus(_aria, StatBonusType.Strength, amount, TimeSpan.FromSeconds(seconds)));
        Assert.Equal(0, _aria.StrengthBonus);
    }

    [Fact]
    public void WhenTheBonusEnds_HitsAboveTheMaximumGo()
    {
        _bonuses.TryAddBonus(_aria, StatBonusType.Strength, 10, TimeSpan.FromMinutes(2));
        _aria.Hits = 60;

        _timers.Fire(_timers.Timers[0].Id);

        Assert.Equal((0, 50), (_aria.StrengthBonus, _aria.Hits));
        Assert.Equal(0, _bonuses.Bonus(_aria, StatBonusType.Strength));
        Assert.Equal(2, _state.Statuses.Count);
    }

    [Fact]
    public void NightSight_LightsThePlayer_UntilItsTimeIsUp()
    {
        Assert.True(_bonuses.TrySetNightSight(_aria, 13, TimeSpan.FromMinutes(20)));
        Assert.True(_bonuses.HasNightSight(_aria));
        Assert.False(_bonuses.TrySetNightSight(_aria, 13, TimeSpan.FromMinutes(20)));

        _timers.Fire(_timers.Timers[0].Id);

        Assert.False(_bonuses.HasNightSight(_aria));
        Assert.Equal([13, 0], _fixture.Sender.Sent.OfType<PersonalLightLevelPacket>().Select(packet => packet.Level));
    }

    [Fact]
    public void Leaving_EndsEveryEffect()
    {
        _bonuses.TryAddBonus(_aria, StatBonusType.Strength, 10, TimeSpan.FromMinutes(2));
        _bonuses.TrySetNightSight(_aria, 13, TimeSpan.FromMinutes(20));

        _bonuses.OnSessionClosed(_session);

        Assert.Equal(0, _aria.StrengthBonus);
        Assert.False(_bonuses.HasNightSight(_aria));
        Assert.Equal(2, _timers.Unregistered.Count);
    }

    [Fact]
    public void Leaving_AfterTheCharacterLeftTheWorld_StillEndsItsEffects_SoTheNextLoginCanDrinkAgain()
    {
        _bonuses.TryAddBonus(_aria, StatBonusType.Strength, 10, TimeSpan.FromMinutes(2));
        _bonuses.TrySetNightSight(_aria, 13, TimeSpan.FromMinutes(20));
        _fixture.Mobiles.LeaveWorld(_aria.Id);

        _bonuses.OnSessionClosed(_session);

        Assert.Equal(2, _timers.Unregistered.Count);
        Assert.True(_bonuses.TryAddBonus(_aria, StatBonusType.Strength, 10, TimeSpan.FromMinutes(2)));
        Assert.True(_bonuses.TrySetNightSight(_aria, 13, TimeSpan.FromMinutes(20)));
    }

    [Fact]
    public void ABonusShowsTheNewBarToThoseAround_WhenItStartsAndEnds()
    {
        _bonuses.TryAddBonus(_aria, StatBonusType.Strength, 10, TimeSpan.FromMinutes(2));
        _timers.Fire(_timers.Timers[0].Id);

        Assert.Equal([_aria, _aria], _state.HitsSent);
    }

    [Fact]
    public void NightSight_IsSentAgain_WhenThePlayerChangesRegion()
    {
        _bonuses.TrySetNightSight(_aria, 13, TimeSpan.FromMinutes(20));

        _bonuses.RegionChanged(_aria, null, null);

        Assert.Equal([13, 13], _fixture.Sender.Sent.OfType<PersonalLightLevelPacket>().Select(packet => packet.Level));
    }

    [Fact]
    public void EndAll_TakesTheBonusesAway_AndTheHitsAboveTheMaximum()
    {
        _bonuses.TryAddBonus(_aria, StatBonusType.Strength, 10, TimeSpan.FromMinutes(2));
        _aria.Hits = 60;

        _bonuses.EndAll(_aria);

        Assert.Equal((0, 50), (_aria.StrengthBonus, _aria.Hits));
        Assert.Single(_timers.Unregistered);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
