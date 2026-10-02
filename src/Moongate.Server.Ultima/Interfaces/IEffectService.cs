using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Effects;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Plays graphic effects for the players who can see them: those of the same map within the view range. The
///     Enhanced Client gets the particles (0xC7) of an effect that has them; every other client gets the graphic (0xC0),
///     and nothing for an effect of particles only. Called on the game loop.
/// </summary>
public interface IEffectService
{
    /// <summary>
    ///     Plays an animation that stays at <paramref name="location" />, such as the smoke of a teleport.
    /// </summary>
    /// <returns>How many players it was sent to.</returns>
    int PlayAt(MapType map, Point3D location, EffectOptions options);

    /// <summary>
    ///     Plays an animation on the object <paramref name="target" />, which stands at <paramref name="location" />;
    ///     it follows a mobile that moves.
    /// </summary>
    /// <returns>How many players it was sent to.</returns>
    int PlayOn(Serial target, MapType map, Point3D location, EffectOptions options);

    /// <summary>
    ///     Plays an animation flying from one point to another, such as a fireball. A serial names the object at an end
    ///     and is zero for a bare point. It reaches the players in range of either end, once each.
    /// </summary>
    /// <returns>How many players it was sent to.</returns>
    int PlayMoving(MapType map, Serial source, Point3D from, Serial target, Point3D to, EffectOptions options);

    /// <summary>
    ///     Strikes the object <paramref name="target" />, which stands at <paramref name="location" />, with a
    ///     lightning bolt.
    /// </summary>
    /// <returns>How many players it was sent to.</returns>
    int PlayLightning(Serial target, MapType map, Point3D location, Hue hue = default);
}
