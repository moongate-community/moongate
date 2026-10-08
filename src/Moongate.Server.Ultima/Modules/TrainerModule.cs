using System.Diagnostics.CodeAnalysis;
using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>trainer</c> Lua module: what the script of a trainer does with the skills it teaches.
/// </summary>
[ScriptModule("trainer", "Teaches skills for gold, as a trainer does.")]
public sealed class TrainerModule
{
    private readonly ITrainingService _training;
    private readonly IMobileService _mobiles;
    private readonly ISessionService _sessions;
    private readonly IItemService _items;

    public TrainerModule(
        ITrainingService training,
        IMobileService mobiles,
        ISessionService sessions,
        IItemService items
    )
    {
        _training = training;
        _mobiles = mobiles;
        _sessions = sessions;
        _items = items;
    }

    /// <summary>
    ///     Gets the skills the NPC would teach the player more of than it has, as a list of skill ids;
    ///     <c>for _, skill in ipairs(trainer.skills(npc, player)) do ... end</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The skills the NPC teaches that the player knows less of than it would teach: those the NPC has at 60.0 or more, up to a third of its value (42.0 at most), as skill ids in the order of the skills. Empty for an NPC that is not one, a player who is dead or one not in the world."
    )]
    public LuaTable Skills(long npc, long player)
    {
        var table = new LuaTable();

        if (!TryMobile(npc, true, out var trainer) || !TryMobile(player, false, out var student))
        {
            return table;
        }

        var index = 1;

        foreach (var skill in _training.Teachable(trainer, student))
        {
            table[index++] = (int)skill;
        }

        return table;
    }

    /// <summary>
    ///     Has the NPC quote the price of a skill to the player; <c>trainer.quote(npc, player, skill)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Has the NPC quote the price of teaching the skill (a SkillType) to the player, 1 gold for each tenth of a point, and keeps the quote until the player pays it. The NPC says why when it cannot teach. False when nothing was quoted."
    )]
    public bool Quote(long npc, long player, int skill)
    {
        return TryMobile(npc, true, out var trainer) &&
               TryMobile(player, false, out var student) &&
               Enum.IsDefined((SkillType)skill) &&
               _sessions.TryGetByCharacterId(student.Id, out var session) &&
               _training.Quote(session, trainer, (SkillType)skill);
    }

    /// <summary>
    ///     Takes gold dropped on the NPC against the quote; <c>return trainer.pay(npc, giver, item)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Takes the gold item the player dropped on the NPC against the quote the player was given: the skill rises by the gold in tenths of a point, at most what was quoted, and the gold above the price stays with the player. True when gold was taken, which is what an on_drag_drop answers; false for anything else, a player with no quote from this NPC, or a skill that can no longer rise."
    )]
    public bool Pay(long npc, long giver, long item)
    {
        return TryMobile(npc, true, out var trainer) &&
               TryMobile(giver, false, out var student) &&
               item is > 0 and <= uint.MaxValue &&
               _items.TryGet(new Serial((uint)item), out var gold) &&
               _sessions.TryGetByCharacterId(student.Id, out var session) &&
               _training.Pay(session, trainer, gold);
    }

    private bool TryMobile(long serial, bool npc, [NotNullWhen(true)] out MobileEntity? mobile)
    {
        mobile = null;

        return serial is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)serial), out mobile) &&
               mobile.IsNpc == npc;
    }
}
