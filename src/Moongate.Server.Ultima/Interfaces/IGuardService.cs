using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The guards of the guarded regions, called by voice as in ModernUO: a player that says "guards" brings a guard
///     onto every criminal near it that stands in a guarded region. There is no combat yet, so the guard appears, says its line
///     and leaves after a while. Called
///     on the game loop.
/// </summary>
public interface IGuardService
{
    /// <summary>
    ///     A player said something: the guards keyword of its client, or the plain word, calls the guards.
    /// </summary>
    void Heard(MobileEntity speaker, string text, IReadOnlyList<int>? keywords = null);

    /// <summary>
    ///     Calls the guards where <paramref name="caller" /> stands, and returns how many were sent for: one for each
    ///     criminal within range that stands in a guarded region, has none on it and is not staff. None outside a guarded region.
    /// </summary>
    int Call(MobileEntity caller);
}
