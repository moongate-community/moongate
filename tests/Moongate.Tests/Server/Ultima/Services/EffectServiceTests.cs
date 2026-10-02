using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Effects;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Effects;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class EffectServiceTests
{
    private static readonly Point3D Spot = new(100, 100, 0);

    private static readonly EffectOptions Smoke = new() { Graphic = (int)EffectGraphicType.Smoke };

    [Fact]
    public async Task PlayAt_ReachesThePlayersInViewRangeOnTheSameMapOnly()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var near = await fixture.AddAsync(2);
        await fixture.AddAsync(3);
        await fixture.AddAsync(4, map: MapType.Felucca);
        Place(fixture, 2, 118, 100);
        Place(fixture, 3, 119, 100);
        Place(fixture, 4, 100, 100);
        var sent = 0;

        await fixture.Network.ExecuteOnLoopAsync(() => sent = Service(fixture).PlayAt(MapType.Trammel, Spot, Smoke));

        Assert.Equal(1, sent);
        Assert.Equal([near.SessionId], fixture.Sender.SentSessionIds);
        var effect = Assert.IsType<HuedEffectPacket>(Assert.Single(fixture.Sender.Sent)).Effect;
        Assert.Equal(
            (EffectKindType.FixedLocation, (int)EffectGraphicType.Smoke, Spot, Spot, true, false, (byte)10, (byte)10),
            (effect.Kind, effect.Graphic, effect.From, effect.To, effect.FixedDirection, effect.Explodes, effect.Speed,
                effect.Duration)
        );
        Assert.Equal((Serial.Zero, Serial.Zero), (effect.Source, effect.Target));
    }

    [Fact]
    public async Task PlayAt_ReachesAViewerOnTheDiagonal_AsTheClientViewIsASquare()
    {
        // 13 cells east and 13 south is inside the 18-cell view of the client, although 18.4 cells away in a line.
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(2);
        await fixture.AddAsync(3);
        Place(fixture, 2, 118, 118);
        Place(fixture, 3, 119, 100);
        var sent = 0;

        await fixture.Network.ExecuteOnLoopAsync(() => sent = Service(fixture).PlayAt(MapType.Trammel, Spot, Smoke));

        Assert.Equal(1, sent);
    }

    [Fact]
    public async Task AnEffectWithoutParticles_GoesAsItsGraphicToTheEnhancedClientToo()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var enhanced = await fixture.AddAsync(2);
        SetVersion(enhanced, "67.0.117.0");
        Place(fixture, 2, 105, 100);

        await fixture.Network.ExecuteOnLoopAsync(() => Service(fixture).PlayAt(MapType.Trammel, Spot, Smoke));

        Assert.IsType<HuedEffectPacket>(Assert.Single(fixture.Sender.Sent));
    }

    [Fact]
    public async Task PlayMoving_APlaceholderGraphic_IsNotSentToAClassicClient()
    {
        // As ModernUO: scripts pass 1 as the graphic of a moving effect made of particles only.
        await using var fixture = await BroadcastFixture.CreateAsync();
        var classic = await fixture.AddAsync(2);
        SetVersion(classic, "7.0.117.0");
        Place(fixture, 2, 105, 100);
        var sent = 0;

        await fixture.Network.ExecuteOnLoopAsync(
            () => sent = Service(fixture)
                .PlayMoving(MapType.Trammel, new Serial(2), Spot, Serial.Zero, Spot, new EffectOptions { Graphic = 1, Particle = 9502 })
        );

        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task PlayOn_StaysOnTheObject()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(2);
        Place(fixture, 2, 105, 100);
        var options = Smoke with { Hue = new Hue(0x47F), RenderMode = EffectRenderModeType.Translucent, Speed = 9, Duration = 32 };

        await fixture.Network.ExecuteOnLoopAsync(() => Service(fixture).PlayOn(new Serial(0x100), MapType.Trammel, Spot, options));

        var effect = Assert.IsType<HuedEffectPacket>(Assert.Single(fixture.Sender.Sent)).Effect;
        Assert.Equal(
            (EffectKindType.FixedObject, new Serial(0x100), Spot, Spot, true, new Hue(0x47F), EffectRenderModeType.Translucent,
                (byte)9, (byte)32),
            (effect.Kind, effect.Source, effect.From, effect.To, effect.FixedDirection, effect.Hue, effect.RenderMode,
                effect.Speed, effect.Duration)
        );
    }

    [Fact]
    public async Task PlayMoving_ReachesThoseNearTheSourceOrTheDestination_OnceEach()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var atSource = await fixture.AddAsync(2);
        var atDestination = await fixture.AddAsync(3);
        var between = await fixture.AddAsync(4);
        await fixture.AddAsync(5);
        Place(fixture, 2, 90, 100);
        Place(fixture, 3, 140, 100);
        Place(fixture, 4, 115, 100);
        Place(fixture, 5, 170, 100);
        var destination = new Point3D(130, 100, 0);
        var options = new EffectOptions { Graphic = (int)EffectGraphicType.LargeFireball, Speed = 7, Duration = 0, Explodes = true };
        var sent = 0;

        await fixture.Network.ExecuteOnLoopAsync(
            () => sent = Service(fixture).PlayMoving(MapType.Trammel, new Serial(2), Spot, new Serial(0x100), destination, options)
        );

        Assert.Equal(3, sent);
        Assert.Equal(
            new[] { atSource.SessionId, atDestination.SessionId, between.SessionId }.Order(),
            fixture.Sender.SentSessionIds.Order()
        );
        var effect = Assert.IsType<HuedEffectPacket>(fixture.Sender.Sent[0]).Effect;
        Assert.Equal(
            (EffectKindType.Moving, new Serial(2), new Serial(0x100), Spot, destination, false, true),
            (effect.Kind, effect.Source, effect.Target, effect.From, effect.To, effect.FixedDirection, effect.Explodes)
        );
    }

    [Fact]
    public async Task PlayLightning_StrikesTheObject()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(2);
        Place(fixture, 2, 105, 100);

        await fixture.Network.ExecuteOnLoopAsync(
            () => Service(fixture).PlayLightning(new Serial(0x100), MapType.Trammel, Spot, new Hue(0x21))
        );

        // As ModernUO's bolt: kind lightning, the target as the source, no graphic, both points the target's.
        var effect = Assert.IsType<HuedEffectPacket>(Assert.Single(fixture.Sender.Sent)).Effect;
        Assert.Equal(
            (EffectKindType.Lightning, new Serial(0x100), 0, Spot, Spot, new Hue(0x21)),
            (effect.Kind, effect.Source, effect.Graphic, effect.From, effect.To, effect.Hue)
        );
    }

    [Fact]
    public async Task AnEffectWithParticles_GoesAsParticlesToTheEnhancedClientAndAsItsGraphicToTheClassicOne()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var classic = await fixture.AddAsync(2);
        var enhanced = await fixture.AddAsync(3);
        SetVersion(classic, "7.0.117.0");
        SetVersion(enhanced, "67.0.117.0");
        Place(fixture, 2, 105, 100);
        Place(fixture, 3, 106, 100);
        var options = new EffectOptions
        {
            Graphic = (int)EffectGraphicType.SparkleHeal, Speed = 9, Duration = 32, Particle = 5005,
            Layer = EffectLayerType.Waist
        };

        await fixture.Network.ExecuteOnLoopAsync(() => Service(fixture).PlayOn(new Serial(0x100), MapType.Trammel, Spot, options));

        var byId = fixture.Sender.SentSessionIds.Zip(fixture.Sender.Sent).ToDictionary(pair => pair.First, pair => pair.Second);
        Assert.IsType<HuedEffectPacket>(byId[classic.SessionId]);
        var particles = Assert.IsType<ParticleEffectPacket>(byId[enhanced.SessionId]).Effect;
        Assert.Equal(
            (5005, 1, new Serial(0x100), EffectLayerType.Waist),
            (particles.Particle, particles.ExplodeParticle, particles.ParticleSerial, particles.Layer)
        );
    }

    [Fact]
    public async Task AnEffectOfParticlesOnly_IsNotSentToAClassicClient()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var classic = await fixture.AddAsync(2);
        var enhanced = await fixture.AddAsync(3);
        SetVersion(classic, "7.0.117.0");
        SetVersion(enhanced, "67.0.117.0");
        Place(fixture, 2, 105, 100);
        Place(fixture, 3, 106, 100);
        var sent = 0;

        await fixture.Network.ExecuteOnLoopAsync(
            () => sent = Service(fixture).PlayAt(MapType.Trammel, Spot, new EffectOptions { Particle = 2023 })
        );

        Assert.Equal(1, sent);
        Assert.Equal([enhanced.SessionId], fixture.Sender.SentSessionIds);
        Assert.IsType<ParticleEffectPacket>(Assert.Single(fixture.Sender.Sent));
    }

    [Fact]
    public async Task TheViewRangeOfTheConfig_IsTheReach()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(2);
        Place(fixture, 2, 110, 100);
        var service = new EffectService(fixture.Sessions, fixture.Mobiles, fixture.Sender, new WorldConfig { ViewRange = 9 });
        var sent = 0;

        await fixture.Network.ExecuteOnLoopAsync(() => sent = service.PlayAt(MapType.Trammel, Spot, Smoke));

        Assert.Equal(0, sent);
    }

    private static EffectService Service(BroadcastFixture fixture)
    {
        return new(fixture.Sessions, fixture.Mobiles, fixture.Sender, new WorldConfig());
    }

    private static void SetVersion(GameSession session, string version)
    {
        session.NetworkSession.SetClientVersion(ClientVersion.Parse(version));
    }

    private static void Place(BroadcastFixture fixture, uint serial, int x, int y)
    {
        Assert.True(fixture.Mobiles.TryGet(new Serial(serial), out var mobile));
        mobile.Location = new Point3D(x, y, 0);
    }
}
