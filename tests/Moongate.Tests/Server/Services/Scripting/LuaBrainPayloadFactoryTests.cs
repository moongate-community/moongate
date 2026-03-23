using Moongate.Server.Services.Interaction;
using Moongate.Server.Services.Scripting.Internal;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class LuaBrainPayloadFactoryTests
{
    [Test]
    public void BuildInRangeEventPayload_ShouldIncludeSourceIdentityAndReputationFields()
    {
        var listenerMobile = new UOMobileEntity
        {
            Id = (Serial)0x0200u,
            Name = "Guard",
            IsPlayer = false,
            MapId = 1,
            Location = new Point3D(99, 200, 5)
        };
        var sourceMobile = new UOMobileEntity
        {
            Id = (Serial)0x0100u,
            Name = "Zombie",
            IsPlayer = false,
            Fame = 600,
            Karma = -600,
            Notoriety = Notoriety.CanBeAttacked,
            MapId = 1,
            Location = new Point3D(100, 200, 5)
        };

        var payload = LuaBrainPayloadFactory.BuildInRangeEventPayload(
            listenerMobile,
            sourceMobile,
            12,
            new NotorietyService(),
            new AiRelationService()
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(payload["source_mobile_id"], Is.EqualTo((uint)sourceMobile.Id));
                Assert.That(payload["source_name"], Is.EqualTo("Zombie"));
                Assert.That(payload["source_fame"], Is.EqualTo(600));
                Assert.That(payload["source_karma"], Is.EqualTo(-600));
                Assert.That(payload["source_notoriety"], Is.EqualTo(nameof(Notoriety.CanBeAttacked)));
                Assert.That(payload["source_is_enemy"], Is.EqualTo(true));
                Assert.That(payload["source_relation"], Is.EqualTo(nameof(AiRelation.Hostile)));
            }
        );
    }

    [Test]
    public void BuildInRangeEventPayload_ShouldTreatInnocentPlayerAsNonEnemy_ForGuardListener()
    {
        var listenerMobile = new UOMobileEntity
        {
            Id = (Serial)0x0200u,
            Name = "Guard",
            IsPlayer = false,
            MapId = 1,
            Location = new Point3D(99, 200, 5)
        };
        var sourceMobile = new UOMobileEntity
        {
            Id = (Serial)0x0101u,
            Name = "Player",
            IsPlayer = true,
            Fame = 100,
            Karma = 100,
            Notoriety = Notoriety.Innocent,
            MapId = 1,
            Location = new Point3D(100, 200, 5)
        };

        var payload = LuaBrainPayloadFactory.BuildInRangeEventPayload(
            listenerMobile,
            sourceMobile,
            12,
            new NotorietyService(),
            new AiRelationService()
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(payload["source_notoriety"], Is.EqualTo(nameof(Notoriety.Innocent)));
                Assert.That(payload["source_is_enemy"], Is.EqualTo(false));
                Assert.That(payload["source_relation"], Is.EqualTo(nameof(AiRelation.Neutral)));
            }
        );
    }

    [Test]
    public void BuildInRangeEventPayload_ShouldIgnoreExpiredAggression_ForGuardListener()
    {
        var listenerMobile = new UOMobileEntity
        {
            Id = (Serial)0x0200u,
            Name = "Guard",
            IsPlayer = false,
            MapId = 1,
            Location = new Point3D(99, 200, 5)
        };
        var sourceMobile = new UOMobileEntity
        {
            Id = (Serial)0x0101u,
            Name = "Player",
            IsPlayer = true,
            Fame = 100,
            Karma = 100,
            Notoriety = Notoriety.Innocent,
            MapId = 1,
            Location = new Point3D(100, 200, 5)
        };

        listenerMobile.Aggressors.Add(new(sourceMobile.Id, listenerMobile.Id, DateTime.UtcNow.AddMinutes(-3), false, false));

        var payload = LuaBrainPayloadFactory.BuildInRangeEventPayload(
            listenerMobile,
            sourceMobile,
            12,
            new NotorietyService(),
            new AiRelationService()
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(payload["source_notoriety"], Is.EqualTo(nameof(Notoriety.Innocent)));
                Assert.That(payload["source_is_enemy"], Is.EqualTo(false));
                Assert.That(payload["source_relation"], Is.EqualTo(nameof(AiRelation.Neutral)));
            }
        );
    }
}
