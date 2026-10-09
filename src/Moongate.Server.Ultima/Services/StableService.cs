using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Bank;
using Moongate.Server.Ultima.Types.Stable;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     The stable of a player: see <see cref="IStableService" />.
/// </summary>
public sealed class StableService : IStableService
{
    private const char Separator = ';';
    private const int SpawnAttempts = 3;

    private readonly IMobileService _mobiles;
    private readonly INpcService _npcs;
    private readonly IMobileTemplateService _templates;
    private readonly IBankService _bank;
    private readonly StableConfig _config;
    private readonly IGameLoopService _loop;
    private readonly Lazy<IDeathService>? _death;
    private readonly Lazy<IPetService>? _pets;
    private readonly ILogger _logger;

    /// <summary>
    ///     Gets how long a failed spawn of a claimed pet waits before it is tried again.
    /// </summary>
    internal TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    public StableService(
        IMobileService mobiles,
        INpcService npcs,
        IMobileTemplateService templates,
        IBankService bank,
        StableConfig config,
        IGameLoopService loop,
        Lazy<IDeathService>? death = null,
        ILogger? logger = null,
        Lazy<IPetService>? pets = null
    )
    {
        _pets = pets;
        _mobiles = mobiles;
        _npcs = npcs;
        _templates = templates;
        _bank = bank;
        _config = config;
        _loop = loop;
        _death = death;
        _logger = logger ?? Log.ForContext<StableService>();
    }

    public IReadOnlyList<string> Stabled(MobileEntity player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return player.GetProp(MountProps.Stabled, "")
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public StableResultType TryStable(MobileEntity player, MobileEntity pet)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(pet);

        if (player.IsNpc || !_mobiles.IsInWorld(player.Id))
        {
            return StableResultType.NoPlayer;
        }

        if (!pet.IsNpc ||
            !_mobiles.IsInWorld(pet.Id) ||
            pet.TemplateId is not { } templateId ||
            !_templates.TryGet(templateId, out var template) ||
            template.MountItem() is null)
        {
            return StableResultType.NotAPet;
        }

        if (pet.GetProp(MountProps.Owner, 0L) != player.Id.Value)
        {
            return StableResultType.NotYours;
        }

        if (player.Map != pet.Map || !MountService.WithinReach(player.Location, pet.Location))
        {
            return StableResultType.TooFar;
        }

        if (_death?.Value.IsDying(pet.Id) == true)
        {
            return StableResultType.Dying;
        }

        var stabled = Stabled(player).ToList();

        if (stabled.Count >= _config.MaxPets)
        {
            return StableResultType.Full;
        }

        if (_config.Fee > 0 && !PayFee(player))
        {
            return StableResultType.NoGold;
        }

        // The creature goes last: when it cannot, the fee is given back.
        if (!_npcs.Remove(pet.Id))
        {
            if (_config.Fee > 0 && _bank.GiveGold(player, _config.Fee) != BankResultType.Ok)
            {
                _logger.Error(
                    "The fee of {Fee} gold could not be given back to {Player:l} after the pet could not be stabled",
                    _config.Fee,
                    player.Id
                );
            }

            return StableResultType.Failed;
        }

        var loyalties = Loyalties(player, stabled.Count);
        stabled.Add(templateId);
        loyalties.Add(pet.GetProp(MountProps.PetLoyalty, PetService.MaxLoyalty));
        Keep(player, stabled, loyalties);
        _pets?.Value.Changed(player.Id);

        return StableResultType.Ok;
    }

    public StableResultType TryClaim(MobileEntity player, int index, string template)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(template);

        if (player.IsNpc || !_mobiles.IsInWorld(player.Id))
        {
            return StableResultType.NoPlayer;
        }

        var stabled = Stabled(player).ToList();

        if (index < 0 || index >= stabled.Count || !string.Equals(stabled[index], template, StringComparison.Ordinal))
        {
            return StableResultType.BadIndex;
        }

        var loyalties = Loyalties(player, stabled.Count);
        var loyalty = loyalties[index];
        stabled.RemoveAt(index);
        loyalties.RemoveAt(index);
        Keep(player, stabled, loyalties);

        if (!_templates.TryGet(template, out _))
        {
            _logger.Warning("The stabled {Template:l} of {Player:l} is gone from the data and is dropped", template, player.Id);

            return StableResultType.Failed;
        }

        var map = player.Map;
        var location = player.Location;
        var props = new Dictionary<string, object?>
        {
            [MountProps.Owner] = (long)player.Id.Value, [MountProps.PetLoyalty] = loyalty
        };

        // Off the loop: a new creature is saved first, to get its serial.
        _ = Task.Run(() => SpawnAsync(player.Id, template, map, location, props, loyalty));
        _pets?.Value.Changed(player.Id);

        return StableResultType.Ok;
    }

    private bool PayFee(MobileEntity player)
    {
        return _bank.CanPay(player, _config.Fee, true) == BankResultType.Ok &&
               _bank.Pay(player, _config.Fee, true, out _) == BankResultType.Ok;
    }

    // The loyalty of each stabled pet, one for each of the count, 100 where none is kept.
    private static List<int> Loyalties(MobileEntity player, int count)
    {
        var kept = player.GetProp(MountProps.StabledLoyalty, "")
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(text => int.TryParse(text, out var value) ? Math.Clamp(value, 0, PetService.MaxLoyalty) : PetService.MaxLoyalty)
            .Take(count)
            .ToList();

        while (kept.Count < count)
        {
            kept.Add(PetService.MaxLoyalty);
        }

        return kept;
    }

    private static void Keep(MobileEntity player, List<string> stabled, List<int> loyalties)
    {
        if (stabled.Count == 0)
        {
            player.RemoveProp(MountProps.Stabled);
            player.RemoveProp(MountProps.StabledLoyalty);

            return;
        }

        player.SetProp(MountProps.Stabled, string.Join(Separator, stabled));
        player.SetProp(MountProps.StabledLoyalty, string.Join(Separator, loyalties));
    }

    private async Task SpawnAsync(
        Serial player,
        string template,
        MapType map,
        Point3D location,
        IReadOnlyDictionary<string, object?> props,
        int loyalty
    )
    {
        for (var attempt = 1; attempt <= SpawnAttempts; attempt++)
        {
            try
            {
                await _npcs.SpawnAsync(template, map, location, props);

                return;
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    "The stabled {Template:l} could not be made again at {Location:l} (attempt {Attempt} of {Attempts})",
                    template,
                    location,
                    attempt,
                    SpawnAttempts
                );

                if (attempt < SpawnAttempts)
                {
                    await Task.Delay(RetryDelay);
                }
            }
        }

        // The pet goes back to the stable, where the player can claim it again.
        await _loop.PostAsync(new LoopActionWorkItem(() => Restore(player, template, loyalty)));
    }

    // On the game loop: a pet that could not be made is put back at the end of the list, when its player is still here.
    private void Restore(Serial player, string template, int loyalty)
    {
        if (!_mobiles.TryGet(player, out var owner))
        {
            _logger.Error("The stabled {Template:l} of {Player:l} is lost: the player left before it was put back", template, player);

            return;
        }

        var stabled = Stabled(owner).ToList();
        var loyalties = Loyalties(owner, stabled.Count);
        stabled.Add(template);
        loyalties.Add(loyalty);
        Keep(owner, stabled, loyalties);
        _logger.Warning("The stabled {Template:l} of {Player:l} is back in the stable", template, player);
    }
}
