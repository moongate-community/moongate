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

        var payload = LuaBrainPayloadFactory.BuildInRangeEventPayload((Serial)0x0200u, sourceMobile, 12);

        Assert.Multiple(
            () =>
            {
                Assert.That(payload["source_mobile_id"], Is.EqualTo((uint)sourceMobile.Id));
                Assert.That(payload["source_name"], Is.EqualTo("Zombie"));
                Assert.That(payload["source_fame"], Is.EqualTo(600));
                Assert.That(payload["source_karma"], Is.EqualTo(-600));
                Assert.That(payload["source_notoriety"], Is.EqualTo(nameof(Notoriety.CanBeAttacked)));
                Assert.That(payload["source_is_enemy"], Is.EqualTo(true));
            }
        );
    }
}
