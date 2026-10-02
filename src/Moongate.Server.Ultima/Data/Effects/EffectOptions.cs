using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Types.Effects;

namespace Moongate.Server.Ultima.Data.Effects;

/// <summary>
///     What an effect looks like, whatever its kind: the graphic and how it is played, and the particles the Enhanced
///     Client adds.
/// </summary>
public sealed record EffectOptions
{
    /// <summary>
    ///     The art id of the animation, such as an <see cref="EffectGraphicType" />; 0 for an effect of particles only,
    ///     which only the Enhanced Client shows.
    /// </summary>
    public int Graphic { get; init; }

    /// <summary>
    ///     How fast the animation plays or moves; 10 as ModernUO's default.
    /// </summary>
    public byte Speed { get; init; } = 10;

    /// <summary>
    ///     How long the animation lasts.
    /// </summary>
    public byte Duration { get; init; } = 10;

    public Hue Hue { get; init; }

    public EffectRenderModeType RenderMode { get; init; }

    /// <summary>
    ///     Whether a moving effect keeps its direction instead of turning towards the target.
    /// </summary>
    public bool FixedDirection { get; init; }

    /// <summary>
    ///     Whether a moving effect explodes when it arrives.
    /// </summary>
    public bool Explodes { get; init; }

    /// <summary>
    ///     The particle effect id; 0 for none.
    /// </summary>
    public int Particle { get; init; }

    /// <summary>
    ///     The particle effect played when a moving effect arrives.
    /// </summary>
    public int ExplodeParticle { get; init; }

    /// <summary>
    ///     The sound played when a moving effect arrives.
    /// </summary>
    public int ExplodeSound { get; init; }

    /// <summary>
    ///     The body part the particles of an effect on a mobile are shown at.
    /// </summary>
    public EffectLayerType Layer { get; init; } = EffectLayerType.None;
}
