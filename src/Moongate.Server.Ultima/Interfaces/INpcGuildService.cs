using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Guilds;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The guilds of the trades, as ModernUO's guildmasters keep them: a player pays 500 gold to a guildmaster to join its
///     guild, belongs to one at most, and can resign a week after joining. The guild is a prop of the character, saved
///     with it. Game loop only.
/// </summary>
public interface INpcGuildService
{
    /// <summary>
    ///     Gets the guild a guildmaster takes members for, from its mobile template; null for an NPC that is none.
    /// </summary>
    NpcGuildType? Of(MobileEntity guildmaster);

    /// <summary>
    ///     Gets the guild a player belongs to; null for none.
    /// </summary>
    NpcGuildType? MemberOf(MobileEntity player);

    /// <summary>
    ///     Has the guildmaster tell the player the price of joining, or why it cannot: already a member, in another guild,
    ///     or a requirement of the guild not met (the thieves want no kills and Stealing 60.0).
    /// </summary>
    /// <returns>True when the price was told.</returns>
    bool Quote(MobileEntity guildmaster, MobileEntity player);

    /// <summary>
    ///     Takes gold dropped on the guildmaster as the price of joining: only a pile of exactly the price, from a player
    ///     who may join, is taken. The guildmaster tells the player welcome, or why not.
    /// </summary>
    /// <returns>True when the player joined and the gold was taken.</returns>
    bool Join(MobileEntity guildmaster, MobileEntity player, ItemEntity gold);

    /// <summary>
    ///     Lets the player leave the guild of the guildmaster, no sooner than a week after joining.
    /// </summary>
    /// <returns>True when the player left.</returns>
    bool Resign(MobileEntity guildmaster, MobileEntity player);
}
