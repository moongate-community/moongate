using Moongate.Generators.Annotations.Persistence;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity(global::Moongate.UO.Data.Persistence.PersistenceCoreEntityTypeIds.Item, "item", 1, typeof(global::Moongate.UO.Data.Ids.Serial))]
public partial class UOItemEntity
{
    [MoongatePersistedMember(1, SnapshotName = "Location")]
    public PersistencePoint3D PersistedLocation
    {
        get => PersistencePoint3D.FromPoint3D(Location);
        set => Location = value?.ToPoint3D() ?? global::Moongate.UO.Data.Geometry.Point3D.Zero;
    }

    [MoongatePersistedMember(5, SnapshotName = "Amount")]
    public int PersistedAmount
    {
        get => Amount <= 0 ? 1 : Amount;
        set => Amount = value <= 0 ? 1 : value;
    }

    [MoongatePersistedMember(17, SnapshotName = "ContainerPosition")]
    public PersistencePoint2D PersistedContainerPosition
    {
        get => PersistencePoint2D.FromPoint2D(ContainerPosition);
        set => ContainerPosition = value?.ToPoint2D() ?? global::Moongate.UO.Data.Geometry.Point2D.Zero;
    }

    [MoongatePersistedMember(21, SnapshotName = "CustomProperties")]
    public List<PersistedItemCustomPropertyEntry> PersistedCustomProperties
    {
        get =>
        [
            .. CustomProperties.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                               .Select(
                                   static pair => new PersistedItemCustomPropertyEntry
                                   {
                                       Key = pair.Key,
                                       Property = new()
                                       {
                                           Type = pair.Value.Type,
                                           IntegerValue = pair.Value.IntegerValue,
                                           BooleanValue = pair.Value.BooleanValue,
                                           DoubleValue = pair.Value.DoubleValue,
                                           StringValue = pair.Value.StringValue
                                       }
                                   }
                               )
        ];
        set
        {
            ClearCustomProperties();

            foreach (var entry in value)
            {
                SetCustomProperty(entry.Key, entry.Property);
            }
        }
    }
}
