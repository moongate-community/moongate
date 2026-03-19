using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Persistence.Data.Internal;

internal static class PersistenceCoreDescriptors
{
    public static void EnsureRegistered(IPersistenceEntityRegistry registry)
    {
        if (!registry.IsRegistered<UOAccountEntity, Serial>())
        {
            registry.Register(
                new PersistenceEntityDescriptor<UOAccountEntity, Serial, AccountSnapshot>(
                    PersistenceCoreEntityTypeIds.Account,
                    "account",
                    1,
                    static entity => entity.Id,
                    SnapshotMapper.ToAccountSnapshot,
                    SnapshotMapper.ToAccountEntity,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }

        if (!registry.IsRegistered<UOMobileEntity, Serial>())
        {
            registry.Register(
                new PersistenceEntityDescriptor<UOMobileEntity, Serial, MobileSnapshot>(
                    PersistenceCoreEntityTypeIds.Mobile,
                    "mobile",
                    1,
                    static entity => entity.Id,
                    SnapshotMapper.ToMobileSnapshot,
                    SnapshotMapper.ToMobileEntity,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }

        if (!registry.IsRegistered<UOItemEntity, Serial>())
        {
            registry.Register(
                new PersistenceEntityDescriptor<UOItemEntity, Serial, ItemSnapshot>(
                    PersistenceCoreEntityTypeIds.Item,
                    "item",
                    1,
                    static entity => entity.Id,
                    SnapshotMapper.ToItemSnapshot,
                    SnapshotMapper.ToItemEntity,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }

        if (!registry.IsRegistered<BulletinBoardMessageEntity, Serial>())
        {
            registry.Register(
                new PersistenceEntityDescriptor<BulletinBoardMessageEntity, Serial, BulletinBoardMessageSnapshot>(
                    PersistenceCoreEntityTypeIds.BulletinBoardMessage,
                    "bulletin-board-message",
                    1,
                    static entity => entity.MessageId,
                    SnapshotMapper.ToBulletinBoardMessageSnapshot,
                    SnapshotMapper.ToBulletinBoardMessageEntity,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }

        if (!registry.IsRegistered<HelpTicketEntity, Serial>())
        {
            registry.Register(
                new PersistenceEntityDescriptor<HelpTicketEntity, Serial, HelpTicketSnapshot>(
                    PersistenceCoreEntityTypeIds.HelpTicket,
                    "help-ticket",
                    1,
                    static entity => entity.Id,
                    SnapshotMapper.ToHelpTicketSnapshot,
                    SnapshotMapper.ToHelpTicketEntity,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }
    }
}
