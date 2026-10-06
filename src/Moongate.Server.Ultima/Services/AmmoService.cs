using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Items;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Spends the ammunition of the players that shoot, through the item handling service so the stack is shown smaller,
///     and puts back on the ground the fraction that is not lost.
/// </summary>
public sealed class AmmoService : IAmmoService
{
    /// <summary>
    ///     The item template of an arrow.
    /// </summary>
    public const string ArrowTemplate = "0x0f3f_arrow";

    /// <summary>
    ///     The item template of a crossbow bolt.
    /// </summary>
    public const string BoltTemplate = "0x1bfb_crossbow_bolt";

    /// <summary>
    ///     The percent of the shots whose ammunition is found again.
    /// </summary>
    public const int RecoveryPercent = 40;

    private readonly ICombatGearService _gear;
    private readonly IItemHandlingService _handling;
    private readonly IItemService _items;
    private readonly IWorldViewService _view;
    private readonly Random _random;

    public AmmoService(
        ICombatGearService gear,
        IItemHandlingService handling,
        IItemService items,
        IWorldViewService view,
        Random? random = null
    )
    {
        _gear = gear;
        _handling = handling;
        _items = items;
        _view = view;
        _random = random ?? Random.Shared;
    }

    public bool Spend(MobileEntity shooter, WeaponInfo weapon)
    {
        return _gear.AmmoOf(shooter, weapon) is { } ammo && _handling.Consume(ammo);
    }

    public void Recover(MobileEntity target, WeaponInfo weapon)
    {
        if (weapon.Type is not (WeaponType.Bow or WeaponType.Crossbow) || _random.Next(100) >= RecoveryPercent)
        {
            return;
        }

        var template = weapon.Type == WeaponType.Bow ? ArrowTemplate : BoltTemplate;

        if (_handling.Make(template, 1) is not { } ammo)
        {
            return;
        }

        ammo.PlaceOnGround(target.Map, target.Location);
        _items.Add([ammo]);
        _view.ItemAppeared(ammo);
    }
}
