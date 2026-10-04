using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     How full the players are, from 0 (starving) to 20 (full), as ModernUO counts it: a player in the world loses a
///     point every few minutes, is told when it gets hungry, and with an empty stomach gets no hit points back. Eating
///     raises it. Thirst is counted the same way: it drops at the same pace, drinking raises it, and a parched player
///     gets no stamina back. Called on the game loop.
/// </summary>
public interface IHungerService
{
    /// <summary>
    ///     Sets how full the mobile is, kept from 0 to 20.
    /// </summary>
    void Set(MobileEntity mobile, int hunger);

    /// <summary>
    ///     Sets how quenched the mobile is, kept from 0 to 20.
    /// </summary>
    void SetThirst(MobileEntity mobile, int thirst);
}
