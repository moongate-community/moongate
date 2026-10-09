using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Makes the pets less loyal: one repeating <c>pet_loyalty</c> timer takes <c>ultima.pets.loyalty_drain</c> off every
///     owned creature of the world whose owner is in the world each <c>ultima.pets.loyalty_drain_minutes</c>, as ModernUO's loyalty timer; a pet whose owner is away keeps its loyalty. Below <see cref="WarningBelow" /> a pet looks around desperately; at 0 it has decided it is
///     better off without a master and is wild again. A restart starts the wait again. Pets in a stable or ridden are not in
///     the world, so they do not lose any.
/// </summary>
public sealed class PetLoyaltyService : IPetLoyaltyService, IMoongateStartupService
{
    public const string TimerName = "pet_loyalty";
    public const int WarningBelow = 10;
    public const int DesperateMessage = 1043270;
    public const int WildMessage = 1043255;

    private readonly ILogger _logger = Log.ForContext<PetLoyaltyService>();
    private readonly ITimerService _timers;
    private readonly IMobileService _mobiles;
    private readonly IPetService _pets;
    private readonly ISpeechService _speech;
    private readonly PetsConfig _config;

    private string? _timerId;

    public PetLoyaltyService(
        ITimerService timers,
        IMobileService mobiles,
        IPetService pets,
        ISpeechService speech,
        PetsConfig config
    )
    {
        _timers = timers;
        _mobiles = mobiles;
        _pets = pets;
        _speech = speech;
        _config = config;
    }

    public Task StartAsync()
    {
        var interval = TimeSpan.FromMinutes(_config.LoyaltyDrainMinutes);
        _timerId = _timers.RegisterTimer(TimerName, interval, Drain, interval, true);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_timerId is { } id)
        {
            _timers.UnregisterTimer(id);
            _timerId = null;
        }

        return Task.CompletedTask;
    }

    // A timer callback that throws closes the timer wheel: one bad pet must not stop the server or the pets after it.
    public void Drain()
    {
        try
        {
            foreach (var pet in _mobiles.Mobiles.Where(mobile => mobile.IsNpc).ToArray())
            {
                DrainOne(pet);
            }
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "The loyalty of the pets could not be drained");
        }
    }

    private void DrainOne(MobileEntity pet)
    {
        try
        {
            var owner = pet.GetProp(MountProps.Owner, 0L);

            // A pet whose owner is away is left as it is, as ModernUO takes the pets off the map with their owner.
            if (owner is <= 0 or > uint.MaxValue || !_mobiles.TryGet(new Serial((uint)owner), out _))
            {
                return;
            }

            var loyalty = _pets.AdjustLoyalty(pet, -_config.LoyaltyDrain);

            if (loyalty <= 0)
            {
                _speech.SayCliloc(pet, WildMessage, pet.Name ?? string.Empty);
                _pets.LetGo(pet);
            }
            else if (loyalty < WarningBelow)
            {
                _speech.SayCliloc(pet, DesperateMessage, pet.Name ?? string.Empty);
            }
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "The loyalty of pet {Pet} could not be drained", pet.Id);
        }
    }
}
