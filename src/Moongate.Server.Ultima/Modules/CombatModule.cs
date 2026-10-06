using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

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

    public CombatModule(ICombatService combat, IMobileService mobiles)
    {
        _combat = combat;
        _mobiles = mobiles;
    }

    /// <summary>
    ///     Makes <paramref name="attacker" /> fight <paramref name="target" />; <c>combat.attack(npc, player)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Makes the first mobile fight the second: it swings at it, by the swing timer, while the other is within reach. A player goes into war mode and is told whom it fights; one that attacks an innocent who is not fighting it is a criminal. False when either is not in the world, they are on another map, or the second is hidden, out of view or out of sight of the first. The fight ends when the target is gone or dead, after 60 seconds without a swing (ultima.combat.combatant_seconds), by combat.stop or, for a player, by peace.")]
    public bool Attack(long attacker, long target)
    {
        return TryGet(attacker, out var who) && TryGet(target, out var other) && _combat.Attack(who, other);
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
    [ScriptFunction(helpText: "How far, in cells, the mobile's blows reach: the range of the bow (10) or crossbow (8) an NPC holds, else 1, the melee range of ultima.combat.max_range. nil for a mobile not in the world.")]
    public int? Range(long mobile)
    {
        return TryGet(mobile, out var who) ? _combat.RangeOf(who) : null;
    }

    private bool TryGet(long serial, out MobileEntity mobile)
    {
        mobile = null!;

        return serial is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)serial), out mobile!) &&
               _mobiles.IsInWorld(mobile.Id);
    }
}
