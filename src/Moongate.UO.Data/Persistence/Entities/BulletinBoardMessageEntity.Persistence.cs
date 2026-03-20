using Moongate.Generators.Annotations.Persistence;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity(global::Moongate.UO.Data.Persistence.PersistenceCoreEntityTypeIds.BulletinBoardMessage, "bulletin-board-message", 1, typeof(global::Moongate.UO.Data.Ids.Serial))]
public sealed partial class BulletinBoardMessageEntity
{
    [MoongatePersistedMember(7, SnapshotName = "BodyLines")]
    public string[] PersistedBodyLines
    {
        get => [.. BodyLines];
        set
        {
            BodyLines.Clear();

            if (value.Length > 0)
            {
                BodyLines.AddRange(value);
            }
        }
    }
}
