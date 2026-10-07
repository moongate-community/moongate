using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The blood a hit leaves on the ground: a piece under the one hit and some around it, that go away after a few
///     seconds, as ModernUO's and Source-X's.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface IBloodService
{
    /// <summary>
    ///     Leaves the blood of a hit that did damage where the target stands: one piece under it and, from one to
    ///     <c>blood_pieces</c>, around it, within one tile. Nothing when the blood is off in the configuration or the
    ///     creature does not bleed (<c>blood_hue = -1</c> in its template).
    /// </summary>
    void Splash(MobileEntity target);
}
