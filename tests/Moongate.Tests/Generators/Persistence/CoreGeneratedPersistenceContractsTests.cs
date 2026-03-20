using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Generators.Persistence;

public sealed class CoreGeneratedPersistenceContractsTests
{
    [TestCase(typeof(UOAccountEntity), "Moongate.UO.Data.Persistence.Entities.UOAccountEntitySnapshot", "UOAccountEntityPersistence")]
    [TestCase(typeof(UOMobileEntity), "Moongate.UO.Data.Persistence.Entities.UOMobileEntitySnapshot", "UOMobileEntityPersistence")]
    [TestCase(typeof(UOItemEntity), "Moongate.UO.Data.Persistence.Entities.UOItemEntitySnapshot", "UOItemEntityPersistence")]
    [TestCase(typeof(BulletinBoardMessageEntity), "Moongate.UO.Data.Persistence.Entities.BulletinBoardMessageEntitySnapshot", "BulletinBoardMessageEntityPersistence")]
    [TestCase(typeof(HelpTicketEntity), "Moongate.UO.Data.Persistence.Entities.HelpTicketEntitySnapshot", "HelpTicketEntityPersistence")]
    public void CoreEntities_ShouldExposeGeneratedPersistenceContracts(
        Type entityType,
        string snapshotTypeName,
        string persistenceTypeName
    )
    {
        var assembly = entityType.Assembly;
        var snapshotType = assembly.GetType(snapshotTypeName);
        var persistenceType = assembly.GetType($"Moongate.UO.Data.Persistence.Entities.{persistenceTypeName}");

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshotType, Is.Not.Null, $"Missing generated snapshot type '{snapshotTypeName}'.");
                Assert.That(persistenceType, Is.Not.Null, $"Missing generated persistence type '{persistenceTypeName}'.");
            }
        );
    }
}
