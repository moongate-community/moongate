using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Death;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Kills NPCs: the corpse with what the NPC carried, the death shown and heard around, the NPC gone from the world.
///     Players do not die yet.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface IDeathService
{
    /// <summary>
    ///     Kills the NPC. Where it stood lies its corpse, facing the way it faced, with the contents of its backpack and
    ///     what it wore; the backpack itself, hair, what cannot move and newbied or blessed items go with the NPC. The
    ///     players around see it die and hear its death sound, its script runs <c>on_death(npc, corpse, killer)</c>,
    ///     then it leaves the world, and its spawn region is free to bring another. False, and nothing happens, for a
    ///     player or a mobile that is not in the world.
    /// </summary>
    bool Kill(MobileEntity mobile, MobileEntity? killer = null);

    /// <summary>
    ///     Raises who died from its corpse: an NPC of the same mobile template is born where the corpse lies, with the
    ///     name and the facing of who died and, when it had one, its spawn region; a human body rises with its fall
    ///     played backwards. It comes with the equipment of its template, and the corpse is gone with what was left
    ///     inside. Nothing happens for what is not a corpse on the ground, or for a corpse that names no template that
    ///     exists. Call it off the game loop: the birth of an NPC waits for the database.
    /// </summary>
    Task<ResurrectResult> ResurrectAsync(Serial corpse, CancellationToken cancellationToken = default);
}
