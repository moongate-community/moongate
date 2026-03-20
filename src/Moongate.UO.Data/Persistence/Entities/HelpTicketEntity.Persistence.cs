using Moongate.Generators.Annotations.Persistence;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity(global::Moongate.UO.Data.Persistence.PersistenceCoreEntityTypeIds.HelpTicket, "help-ticket", 1, typeof(global::Moongate.UO.Data.Ids.Serial))]
public sealed partial class HelpTicketEntity
{
    [MoongatePersistedMember(6, SnapshotName = "Location")]
    public PersistencePoint3D PersistedLocation
    {
        get => PersistencePoint3D.FromPoint3D(Location);
        set => Location = value?.ToPoint3D() ?? global::Moongate.UO.Data.Geometry.Point3D.Zero;
    }
}
