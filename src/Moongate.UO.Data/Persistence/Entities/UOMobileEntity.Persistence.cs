using Moongate.Generators.Annotations.Persistence;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity(global::Moongate.UO.Data.Persistence.PersistenceCoreEntityTypeIds.Mobile, "mobile", 1, typeof(global::Moongate.UO.Data.Ids.Serial))]
public partial class UOMobileEntity
{
    [MoongatePersistedMember(5, SnapshotName = "Location")]
    public PersistencePoint3D PersistedLocation
    {
        get => PersistencePoint3D.FromPoint3D(Location);
        set => Location = value?.ToPoint3D() ?? global::Moongate.UO.Data.Geometry.Point3D.Zero;
    }

    [MoongatePersistedMember(18, SnapshotName = "BaseBodyId")]
    public int? PersistedBaseBodyId
    {
        get => BaseBody is null ? null : (int)BaseBody.Value;
        set => BaseBody = value is null ? null : (global::Moongate.UO.Data.Bodies.Body)value.Value;
    }

    [MoongatePersistedMember(37, SnapshotName = "EquippedItems")]
    public List<PersistedEquippedItemEntry> PersistedEquippedItems
    {
        get =>
        [
            .. EquippedItemIds.OrderBy(static pair => (int)pair.Key)
                              .Select(
                                  static pair => new PersistedEquippedItemEntry
                                  {
                                      Layer = pair.Key,
                                      ItemId = (uint)pair.Value
                                  }
                              )
        ];
        set
        {
            EquippedItemIds.Clear();

            foreach (var entry in value)
            {
                EquippedItemIds[entry.Layer] = (global::Moongate.UO.Data.Ids.Serial)entry.ItemId;
            }
        }
    }

    [MoongatePersistedMember(38, SnapshotName = "CustomProperties")]
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

    [MoongatePersistedMember(53, SnapshotName = "Skills")]
    public List<PersistedMobileSkillEntry> PersistedSkills
    {
        get =>
        [
            .. Skills.OrderBy(static pair => (int)pair.Key)
                     .Select(
                         static pair => new PersistedMobileSkillEntry
                         {
                             SkillId = pair.Key,
                             Value = pair.Value.Value,
                             Base = pair.Value.Base,
                             Cap = pair.Value.Cap,
                             Lock = pair.Value.Lock
                         }
                     )
        ];
        set
        {
            Skills.Clear();

            foreach (var entry in value)
            {
                var skill = SetSkill(entry.SkillId, (int)entry.Value, (int)entry.Base, entry.Cap, entry.Lock);
                skill.Value = entry.Value;
                skill.Base = entry.Base;
            }
        }
    }

    [MoongatePersistedMember(54, SnapshotName = "Sounds")]
    public List<PersistedMobileSoundEntry> PersistedSounds
    {
        get =>
        [
            .. Sounds.OrderBy(static pair => (int)pair.Key)
                     .Select(
                         static pair => new PersistedMobileSoundEntry
                         {
                             SoundType = pair.Key,
                             SoundId = pair.Value
                         }
                     )
        ];
        set
        {
            Sounds.Clear();

            foreach (var entry in value)
            {
                Sounds[entry.SoundType] = entry.SoundId;
            }
        }
    }
}
