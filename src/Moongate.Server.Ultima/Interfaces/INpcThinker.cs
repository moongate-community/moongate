using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Decides what an awake NPC does on each of its thinks; called on the game loop by <see cref="INpcTickService" />.
/// </summary>
public interface INpcThinker
{
    /// <summary>
    ///     Runs one think of <paramref name="npc" />.
    /// </summary>
    void Think(MobileEntity npc);
}
