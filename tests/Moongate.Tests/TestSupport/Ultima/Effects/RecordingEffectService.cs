using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Effects;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Effects;

/// <summary>
///     Records the effects asked for and sends nothing.
/// </summary>
public sealed class RecordingEffectService : IEffectService
{
    public List<(MapType Map, Point3D Location, EffectOptions Options)> At { get; } = [];

    public List<(Serial Target, MapType Map, Point3D Location, EffectOptions Options)> On { get; } = [];

    public List<(MapType Map, Serial Source, Point3D From, Serial Target, Point3D To, EffectOptions Options)>
        Moving { get; } = [];

    public List<(Serial Target, MapType Map, Point3D Location, Hue Hue)> Lightning { get; } = [];

    public int PlayAt(MapType map, Point3D location, EffectOptions options)
    {
        At.Add((map, location, options));

        return 1;
    }

    public int PlayOn(Serial target, MapType map, Point3D location, EffectOptions options)
    {
        On.Add((target, map, location, options));

        return 1;
    }

    public int PlayMoving(MapType map, Serial source, Point3D from, Serial target, Point3D to, EffectOptions options)
    {
        Moving.Add((map, source, from, target, to, options));

        return 1;
    }

    public int PlayLightning(Serial target, MapType map, Point3D location, Hue hue = default)
    {
        Lightning.Add((target, map, location, hue));

        return 1;
    }
}
