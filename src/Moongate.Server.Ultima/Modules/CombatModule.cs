using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Lua;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Extensions;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>combat</c> Lua module: makes a mobile fight another, stops it and tells whom it fights;
///     <c>combat.attack(npc, player)</c>.
/// </summary>
[ScriptModule("combat", "Starts and ends the melee fights of mobiles, and tells whom a mobile fights.")]
public sealed class CombatModule
{
    private readonly ICombatService _combat;
    private readonly IMobileService _mobiles;
    private readonly ICombatGearService? _gear;

    public CombatModule(ICombatService combat, IMobileService mobiles, ICombatGearService? gear = null)
    {
        _gear = gear;
        _combat = combat;
        _mobiles = mobiles;
    }

    /// <summary>
    ///     Makes <paramref name="attacker" /> fight <paramref name="target" />; <c>combat.attack(npc, player)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Makes the first mobile fight the second: it swings at it, by the swing timer, while the other is within reach. A player goes into war mode and is told whom it fights; one that attacks an innocent who is not fighting it is a criminal. False when either is not in the world, they are on another map, or the second is hidden, out of view or out of sight of the first. The fight ends when the target is gone or dead, after 60 seconds without a swing (ultima.combat.combatant_seconds), by combat.stop or, for a player, by peace."
    )]
    public bool Attack(long attacker, long target)
    {
        return TryGet(attacker, out var who) && TryGet(target, out var other) && _combat.Attack(who, other);
    }

    /// <summary>
    ///     Hurts a mobile without a swing, as an explosion does; <c>combat.harm(target, 15, thrower)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Hurts the target by damage without a swing, as an explosion does, with the rules of a blow when an attacker is given: a player who harms an innocent that is not fighting it is a criminal (not for harming itself), the murder report is told, the target shows the hurt and the damage, dies with the attacker as its killer, and an NPC fights back. The attacker may be left out, as for a trap. No war mode, and the attacker fights no one for it. False for a target not in the world, dead or invulnerable, an attacker given but not in the world, or a negative damage."
    )]
    public bool Harm(long target, int damage, long? attacker = null)
    {
        MobileEntity? who = null;

        if (attacker is { } serial && !TryGet(serial, out who))
        {
            return false;
        }

        return TryGet(target, out var other) && _combat.Harm(who, other, damage);
    }

    /// <summary>
    ///     Ends the fight of a mobile; <c>combat.stop(npc)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Ends the fight of the mobile: it swings no more. False for a mobile not in the world.")]
    public bool Stop(long mobile)
    {
        if (!TryGet(mobile, out var who))
        {
            return false;
        }

        _combat.Stop(who);

        return true;
    }

    /// <summary>
    ///     Gets the serial of whom a mobile fights; <c>combat.target(npc)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The serial of whom the mobile fights; nil when it fights no one or is not in the world.")]
    public long? Target(long mobile)
    {
        return TryGet(mobile, out var who) && _combat.TargetOf(who) is { } target ? target.Id.Value : null;
    }

    /// <summary>
    ///     Gets how far a mobile's blows reach; <c>combat.range(archer)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "How far, in cells, the mobile's blows reach: the range of the bow (10) or crossbow (8) an NPC holds, else 1, the melee range of ultima.combat.max_range. nil for a mobile not in the world."
    )]
    public int? Range(long mobile)
    {
        return TryGet(mobile, out var who) ? _combat.RangeOf(who) : null;
    }

    /// <summary>
    ///     Gets the armor rating of what a mobile wears; <c>combat.armor_rating(user)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The armor rating of what the mobile wears, as its status shows it: the best piece of each part of the body, each weighted by its share. 0 for none, and nil for a mobile not in the world."
    )]
    public int? ArmorRating(long mobile)
    {
        return _gear is not null && TryGet(mobile, out var who) ? _gear.ArmorRatingOf(who) : null;
    }

    /// <summary>
    ///     Gets what a mobile fights with; <c>combat.weapon(user).skill</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "What the mobile fights with, as a table { skill, ranged, range, projectile, ammo }: skill is the name of the skill the weapon trains (wrestling for fists), ranged is true for a bow or crossbow, range how far its blows reach, and for a ranged weapon projectile is the graphic that flies and ammo is arrow or bolt. nil for a mobile not in the world."
    )]
    public LuaTable? Weapon(long mobile)
    {
        if (!TryGet(mobile, out var who))
        {
            return null;
        }

        var weapon = _combat.HeldWeaponOf(who);
        var table = new LuaTable();
        table["skill"] = EnumNameUtils.Format(weapon?.Skill ?? SkillType.Wrestling);
        table["ranged"] = weapon?.Type is { IsRanged: true };
        table["range"] = _combat.RangeOf(who);

        if (weapon?.Type is { IsRanged: true } type)
        {
            table["projectile"] = type.Projectile;
            table["ammo"] = type == WeaponType.Crossbow ? "bolt" : "arrow";
        }

        return table;
    }

    /// <summary>
    ///     Plays the swing of what a mobile holds towards a place; <c>combat.swing(user, x, y)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Turns the mobile towards x, y and plays the swing of what it holds, seen by the players in range, with no fight, hit or cost: what practising on a dummy does. False for a mobile not in the world."
    )]
    public bool Swing(long mobile, int x, int y)
    {
        if (!TryGet(mobile, out var who))
        {
            return false;
        }

        _combat.PlaySwing(who, x, y);

        return true;
    }

    /// <summary>
    ///     Takes one arrow or bolt out of a shooter's backpack; <c>combat.spend_ammo(user)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Takes one arrow or bolt out of the backpack of a mobile that holds a bow or crossbow, as a shot does, and shows the stack smaller. False, and nothing taken, when it holds none or has no ammunition."
    )]
    public bool SpendAmmo(long mobile)
    {
        return TryGet(mobile, out var who) && _combat.SpendAmmo(who);
    }

    private bool TryGet(long serial, out MobileEntity mobile)
    {
        // Safe: out parameter; callers read it only when the method returns true.
        mobile = null!;

        // Safe: the out value is only used when the lookup succeeds.
        return serial is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)serial), out mobile!) &&
               _mobiles.IsInWorld(mobile.Id);
    }
}
