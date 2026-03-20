using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Persistence.Data.Internal;

internal static class PersistenceCoreDescriptors
{
    public static void EnsureRegistered(IPersistenceEntityRegistry registry)
    {
        if (!registry.IsRegistered<UOAccountEntity, Serial>())
        {
            registry.Register(
                new PersistenceEntityDescriptor<UOAccountEntity, Serial>(
                    PersistenceCoreEntityTypeIds.Account,
                    "account",
                    1,
                    static entity => entity.Id,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }

        if (!registry.IsRegistered<UOMobileEntity, Serial>())
        {
            registry.Register(
                new PersistenceEntityDescriptor<UOMobileEntity, Serial>(
                    PersistenceCoreEntityTypeIds.Mobile,
                    "mobile",
                    1,
                    static entity => entity.Id,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }

        if (!registry.IsRegistered<UOItemEntity, Serial>())
        {
            registry.Register(
                new PersistenceEntityDescriptor<UOItemEntity, Serial>(
                    PersistenceCoreEntityTypeIds.Item,
                    "item",
                    1,
                    static entity => entity.Id,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }

        if (!registry.IsRegistered<BulletinBoardMessageEntity, Serial>())
        {
            registry.Register(
                new PersistenceEntityDescriptor<BulletinBoardMessageEntity, Serial>(
                    PersistenceCoreEntityTypeIds.BulletinBoardMessage,
                    "bulletin-board-message",
                    1,
                    static entity => entity.MessageId,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }

        if (!registry.IsRegistered<HelpTicketEntity, Serial>())
        {
            registry.Register(
                new PersistenceEntityDescriptor<HelpTicketEntity, Serial>(
                    PersistenceCoreEntityTypeIds.HelpTicket,
                    "help-ticket",
                    1,
                    static entity => entity.Id,
                    PersistenceKeyCodecs.SerializeSerial,
                    PersistenceKeyCodecs.DeserializeSerial
                )
            );
        }
    }
}
