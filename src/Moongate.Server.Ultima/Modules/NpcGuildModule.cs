using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>npcguild</c> Lua module: what a guildmaster's script does with the guild it takes members for.
/// </summary>
[ScriptModule("npcguild", "Takes members for the guild of a trade, as a guildmaster does.")]
public sealed class NpcGuildModule
{
    private readonly INpcGuildService _guilds;
    private readonly IMobileService _mobiles;
    private readonly IItemService _items;

    public NpcGuildModule(INpcGuildService guilds, IMobileService mobiles, IItemService items)
    {
        _guilds = guilds;
        _mobiles = mobiles;
        _items = items;
    }

    /// <summary>
    ///     Gets the guild an NPC takes members for, such as <c>"blacksmiths"</c>; nil for an NPC that is no guildmaster;
    ///     <c>if npcguild.of(serial) then ... end</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The guild the NPC takes members for, as a lowercase name such as blacksmiths, thieves or mages, from the npc_guild of its mobile template; nil for an NPC that is no guildmaster or a serial that is no NPC."
    )]
    public string? Of(long npc)
    {
        return TryMobile(npc, true, out var guildmaster) ? _guilds.Of(guildmaster)?.ToString().ToLowerInvariant() : null;
    }

    /// <summary>
    ///     Gets the guild a player belongs to; nil for none; <c>npcguild.member(player)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The guild the player belongs to, as a lowercase name such as blacksmiths; nil for a player in none, an NPC or a serial that is no mobile."
    )]
    public string? Member(long player)
    {
        return TryMobile(player, false, out var mobile) ? _guilds.MemberOf(mobile)?.ToString().ToLowerInvariant() : null;
    }

    /// <summary>
    ///     Has the guildmaster tell the price of joining; <c>npcguild.quote(npc, player)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Has the guildmaster tell the player the price of joining its guild, 500 gold, or why it cannot: already a member, in another guild, or the requirements of the thieves not met (no kills, Stealing 60.0). False when nothing was told as a price."
    )]
    public bool Quote(long npc, long player)
    {
        return TryMobile(npc, true, out var guildmaster) &&
               TryMobile(player, false, out var mobile) &&
               _guilds.Quote(guildmaster, mobile);
    }

    /// <summary>
    ///     Takes gold dropped on the guildmaster as the price of joining; <c>return npcguild.join(npc, giver, item)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Takes the gold item the player dropped on the guildmaster as the price of joining its guild: a pile of exactly 500 gold from a player who is in no guild and meets the requirements. True when the player joined, which is what an on_drag_drop answers; false for anything else, and the gold goes back."
    )]
    public bool Join(long npc, long giver, long item)
    {
        return TryMobile(npc, true, out var guildmaster) &&
               TryMobile(giver, false, out var mobile) &&
               item is > 0 and <= uint.MaxValue &&
               _items.TryGet(new Serial((uint)item), out var gold) &&
               _guilds.Join(guildmaster, mobile, gold);
    }

    /// <summary>
    ///     Lets the player leave the guild; <c>npcguild.resign(npc, player)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Lets the player leave the guild of the guildmaster, no sooner than a week after joining. The guildmaster says why not: the player is not a member of its guild, or joined too lately. True when the player left."
    )]
    public bool Resign(long npc, long player)
    {
        return TryMobile(npc, true, out var guildmaster) &&
               TryMobile(player, false, out var mobile) &&
               _guilds.Resign(guildmaster, mobile);
    }

    private bool TryMobile(long serial, bool npc, [NotNullWhen(true)] out MobileEntity? mobile)
    {
        mobile = null;

        return serial is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)serial), out mobile) &&
               mobile.IsNpc == npc;
    }
}
