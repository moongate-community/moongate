using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Types.Effects;

namespace Moongate.Server.Ultima.Data.Effects;

/// <summary>
///     One graphic effect as the client plays it: the fields of the effect packets 0xC0 and 0xC7. The particle fields
///     are read only by the Enhanced Client.
/// </summary>
public sealed record GraphicEffect
{
    public EffectKindType Kind { get; init; }

    /// <summary>
    ///     The object the effect starts from or stays on; zero for a point of the map.
    /// </summary>
    public Serial Source { get; init; }

    /// <summary>
    ///     The object a moving effect flies to; zero for a point of the map.
    /// </summary>
    public Serial Target { get; init; }

    /// <summary>
    ///     The art id of the animation, such as an <see cref="EffectGraphicType" />; 0 for an effect of particles only.
    /// </summary>
    public int Graphic { get; init; }

    public Point3D From { get; init; }

    public Point3D To { get; init; }

    public byte Speed { get; init; }

    public byte Duration { get; init; }

    /// <summary>
    ///     Whether the graphic keeps its direction instead of turning towards the target.
    /// </summary>
    public bool FixedDirection { get; init; }

    /// <summary>
    ///     Whether a moving effect explodes when it arrives.
    /// </summary>
    public bool Explodes { get; init; }

    public Hue Hue { get; init; }

    public EffectRenderModeType RenderMode { get; init; }

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
    ///     The object the particles belong to; zero for a moving effect.
    /// </summary>
    public Serial ParticleSerial { get; init; }

    public EffectLayerType Layer { get; init; } = EffectLayerType.None;
}
