using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Types.Clients;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Effects;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Effects;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Builds the effect packets as ModernUO does and sends them to the players in view range of the effect.
/// </summary>
public sealed class EffectService : IEffectService
{
    private const int PlaceholderGraphic = 1;

    // What ModernUO writes as the explode particle of an effect that stays.
    private const int FixedExplodeParticle = 1;

    private readonly ISessionService _sessions;
    private readonly IMobileService _mobiles;
    private readonly IPacketSendService _sender;
    private readonly WorldConfig _world;

    public EffectService(ISessionService sessions, IMobileService mobiles, IPacketSendService sender, WorldConfig world)
    {
        _sessions = sessions;
        _mobiles = mobiles;
        _sender = sender;
        _world = world;
    }

    public int PlayAt(MapType map, Point3D location, EffectOptions options)
    {
        return Send(map, location, null, Fixed(EffectKindType.FixedLocation, Serial.Zero, location, options));
    }

    public int PlayOn(Serial target, MapType map, Point3D location, EffectOptions options)
    {
        return Send(map, location, null, Fixed(EffectKindType.FixedObject, target, location, options));
    }

    public int PlayMoving(MapType map, Serial source, Point3D from, Serial target, Point3D to, EffectOptions options)
    {
        var effect = new GraphicEffect
        {
            Kind = EffectKindType.Moving,
            Source = source,
            Target = target,
            Graphic = options.Graphic,
            From = from,
            To = to,
            Speed = options.Speed,
            Duration = options.Duration,
            FixedDirection = options.FixedDirection,
            Explodes = options.Explodes,
            Hue = options.Hue,
            RenderMode = options.RenderMode,
            Particle = options.Particle,
            ExplodeParticle = options.ExplodeParticle,
            ExplodeSound = options.ExplodeSound,
            Layer = options.Layer
        };

        return Send(map, from, to, effect);
    }

    public int PlayLightning(Serial target, MapType map, Point3D location, Hue hue = default)
    {
        // The bolt has no graphic of its own: the client draws it for the kind.
        var effect = new GraphicEffect
        {
            Kind = EffectKindType.Lightning, Source = target, From = location, To = location, Hue = hue
        };

        return Send(map, location, null, effect);
    }

    // An effect that stays: it does not turn, does not explode, and its particles belong to the object it is on.
    private static GraphicEffect Fixed(EffectKindType kind, Serial target, Point3D location, EffectOptions options)
    {
        return new()
        {
            Kind = kind,
            Source = target,
            Graphic = options.Graphic,
            From = location,
            To = location,
            Speed = options.Speed,
            Duration = options.Duration,
            FixedDirection = true,
            Hue = options.Hue,
            RenderMode = options.RenderMode,
            Particle = options.Particle,
            ExplodeParticle = FixedExplodeParticle,
            ParticleSerial = target,
            Layer = options.Layer
        };
    }

    private int Send(MapType map, Point3D from, Point3D? to, GraphicEffect effect)
    {
        var sent = 0;
        HuedEffectPacket? hued = null;
        ParticleEffectPacket? particles = null;
        // An effect on a hidden mobile, or thrown by one, would give it away.
        _mobiles.TryGet(effect.Source, out var source);

        foreach (var session in _sessions.GetAll())
        {
            if (!session.CharacterId.IsValid ||
                !_mobiles.TryGet(session.CharacterId, out var viewer) ||
                !Sees(viewer, map, from, to) ||
                source?.IsHiddenFrom(viewer.Id, session.AccountType) == true ||
                session.NetworkSession.Client is not { IsConnected: true } connection)
            {
                continue;
            }

            if (effect.Particle != 0 && ShowsParticles(session))
            {
                particles ??= new(effect);

                if (_sender.TrySend(session.SessionId, connection, particles))
                {
                    sent++;
                }
            }
            else if (HasGraphic(effect))
            {
                hued ??= new(effect);

                if (_sender.TrySend(session.SessionId, connection, hued))
                {
                    sent++;
                }
            }
        }

        return sent;
    }

    private bool Sees(MobileEntity viewer, MapType map, Point3D from, Point3D? to)
    {
        return viewer.Map == map &&
               (InView(viewer.Location, from) || to is { } destination && InView(viewer.Location, destination));
    }

    // The client shows a square around the player, so the reach is one too, as for the mobiles and items it is sent.
    private bool InView(Point3D viewer, Point3D point)
    {
        return Math.Abs(viewer.X - point.X) <= _world.ViewRange && Math.Abs(viewer.Y - point.Y) <= _world.ViewRange;
    }

    // The bolt has no graphic and is always drawn. A moving effect needs a real art id: ModernUO scripts pass 1 as the
    // placeholder of a moving effect made of particles only.
    private static bool HasGraphic(GraphicEffect effect)
    {
        return effect.Kind == EffectKindType.Lightning ||
               effect.Graphic > (effect.Kind == EffectKindType.Moving ? PlaceholderGraphic : 0);
    }

    // As ModernUO's particle support in its Detect mode: the classic client draws no particles.
    private static bool ShowsParticles(GameSession session)
    {
        return session.ClientVersion is { Type: ClientType.Enhanced };
    }
}
