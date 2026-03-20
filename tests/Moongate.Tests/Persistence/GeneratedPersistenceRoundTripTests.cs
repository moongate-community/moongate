using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Persistence;

public sealed class GeneratedPersistenceRoundTripTests
{
    [Test]
    public void HelpTicketGeneratedPersistence_ShouldRoundTripEntity()
    {
        var persistenceType = typeof(HelpTicketEntity).Assembly.GetType(
            "Moongate.UO.Data.Persistence.Entities.HelpTicketEntityPersistence"
        );
        Assert.That(persistenceType, Is.Not.Null);

        var toSnapshot = persistenceType!.GetMethod("ToSnapshot", [typeof(HelpTicketEntity)]);
        var snapshotType = typeof(HelpTicketEntity).Assembly.GetType(
            "Moongate.UO.Data.Persistence.Entities.HelpTicketEntitySnapshot"
        );
        var fromSnapshot = persistenceType.GetMethod("FromSnapshot", [snapshotType!]);

        Assert.Multiple(
            () =>
            {
                Assert.That(toSnapshot, Is.Not.Null);
                Assert.That(snapshotType, Is.Not.Null);
                Assert.That(fromSnapshot, Is.Not.Null);
            }
        );

        var entity = new HelpTicketEntity
        {
            Id = (Serial)0x4000004Bu,
            SenderCharacterId = (Serial)0x00000042u,
            SenderAccountId = (Serial)0x00000024u,
            Category = HelpTicketCategory.Question,
            Message = "Stuck near the docks.",
            MapId = 0,
            Location = new Point3D(1443, 1692, 0),
            Status = HelpTicketStatus.Open,
            AssignedToCharacterId = (Serial)0x00000003u,
            AssignedToAccountId = (Serial)0x00000002u,
            CreatedAtUtc = new(2026, 3, 19, 9, 30, 0, DateTimeKind.Utc),
            AssignedAtUtc = new(2026, 3, 19, 9, 45, 0, DateTimeKind.Utc),
            ClosedAtUtc = null,
            LastUpdatedAtUtc = new(2026, 3, 19, 9, 45, 0, DateTimeKind.Utc)
        };

        var snapshot = toSnapshot!.Invoke(null, [entity]);
        var restored = (HelpTicketEntity?)fromSnapshot!.Invoke(null, [snapshot]);

        Assert.That(restored, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(restored!.Id, Is.EqualTo(entity.Id));
                Assert.That(restored.SenderCharacterId, Is.EqualTo(entity.SenderCharacterId));
                Assert.That(restored.SenderAccountId, Is.EqualTo(entity.SenderAccountId));
                Assert.That(restored.Category, Is.EqualTo(entity.Category));
                Assert.That(restored.Message, Is.EqualTo(entity.Message));
                Assert.That(restored.MapId, Is.EqualTo(entity.MapId));
                Assert.That(restored.Location, Is.EqualTo(entity.Location));
                Assert.That(restored.Status, Is.EqualTo(entity.Status));
                Assert.That(restored.AssignedToCharacterId, Is.EqualTo(entity.AssignedToCharacterId));
                Assert.That(restored.AssignedToAccountId, Is.EqualTo(entity.AssignedToAccountId));
                Assert.That(restored.CreatedAtUtc, Is.EqualTo(entity.CreatedAtUtc));
                Assert.That(restored.AssignedAtUtc, Is.EqualTo(entity.AssignedAtUtc));
                Assert.That(restored.ClosedAtUtc, Is.EqualTo(entity.ClosedAtUtc));
                Assert.That(restored.LastUpdatedAtUtc, Is.EqualTo(entity.LastUpdatedAtUtc));
            }
        );
    }
}
