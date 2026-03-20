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
                new PersistenceEntityDescriptor<UOAccountEntity, Serial, UOAccountEntitySnapshot>(
                    UOAccountEntityPersistence.TypeId,
                    UOAccountEntityPersistence.TypeName,
                    UOAccountEntityPersistence.SchemaVersion,
                    UOAccountEntityPersistence.GetKey,
                    UOAccountEntityPersistence.ToSnapshot,
                    UOAccountEntityPersistence.FromSnapshot,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }

        if (!registry.IsRegistered<UOMobileEntity, Serial>())
        {
            registry.Register(
                new PersistenceEntityDescriptor<UOMobileEntity, Serial, UOMobileEntitySnapshot>(
                    UOMobileEntityPersistence.TypeId,
                    UOMobileEntityPersistence.TypeName,
                    UOMobileEntityPersistence.SchemaVersion,
                    UOMobileEntityPersistence.GetKey,
                    UOMobileEntityPersistence.ToSnapshot,
                    UOMobileEntityPersistence.FromSnapshot,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }

        if (!registry.IsRegistered<UOItemEntity, Serial>())
        {
            registry.Register(
                new PersistenceEntityDescriptor<UOItemEntity, Serial, UOItemEntitySnapshot>(
                    UOItemEntityPersistence.TypeId,
                    UOItemEntityPersistence.TypeName,
                    UOItemEntityPersistence.SchemaVersion,
                    UOItemEntityPersistence.GetKey,
                    UOItemEntityPersistence.ToSnapshot,
                    UOItemEntityPersistence.FromSnapshot,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }

        if (!registry.IsRegistered<BulletinBoardMessageEntity, Serial>())
        {
            registry.Register(
                new PersistenceEntityDescriptor<BulletinBoardMessageEntity, Serial, BulletinBoardMessageEntitySnapshot>(
                    BulletinBoardMessageEntityPersistence.TypeId,
                    BulletinBoardMessageEntityPersistence.TypeName,
                    BulletinBoardMessageEntityPersistence.SchemaVersion,
                    BulletinBoardMessageEntityPersistence.GetKey,
                    BulletinBoardMessageEntityPersistence.ToSnapshot,
                    BulletinBoardMessageEntityPersistence.FromSnapshot,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }

        if (!registry.IsRegistered<HelpTicketEntity, Serial>())
        {
            registry.Register(
                new PersistenceEntityDescriptor<HelpTicketEntity, Serial, HelpTicketEntitySnapshot>(
                    HelpTicketEntityPersistence.TypeId,
                    HelpTicketEntityPersistence.TypeName,
                    HelpTicketEntityPersistence.SchemaVersion,
                    HelpTicketEntityPersistence.GetKey,
                    HelpTicketEntityPersistence.ToSnapshot,
                    HelpTicketEntityPersistence.FromSnapshot,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }
    }
}
