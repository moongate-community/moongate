namespace Moongate.Server.PacketHandlers.Generators.Data.Internal;

internal sealed class PersistenceMemberModel : IEquatable<PersistenceMemberModel>
{
    public PersistenceMemberModel(
        int order,
        string memberName,
        string snapshotMemberName,
        string entityTypeName,
        string snapshotTypeName,
        PersistenceMemberKind kind,
        PersistenceCollectionKind collectionKind,
        string? nestedPersistenceTypeName,
        bool isNullable,
        bool isString
    )
    {
        Order = order;
        MemberName = memberName;
        SnapshotMemberName = snapshotMemberName;
        EntityTypeName = entityTypeName;
        SnapshotTypeName = snapshotTypeName;
        Kind = kind;
        CollectionKind = collectionKind;
        NestedPersistenceTypeName = nestedPersistenceTypeName;
        IsNullable = isNullable;
        IsString = isString;
    }

    public int Order { get; }

    public string MemberName { get; }

    public string SnapshotMemberName { get; }

    public string EntityTypeName { get; }

    public string SnapshotTypeName { get; }

    public PersistenceMemberKind Kind { get; }

    public PersistenceCollectionKind CollectionKind { get; }

    public string? NestedPersistenceTypeName { get; }

    public bool IsNullable { get; }

    public bool IsString { get; }

    public bool Equals(PersistenceMemberModel? other)
    {
        if (other is null)
        {
            return false;
        }

        return Order == other.Order &&
               MemberName == other.MemberName &&
               SnapshotMemberName == other.SnapshotMemberName &&
               EntityTypeName == other.EntityTypeName &&
               SnapshotTypeName == other.SnapshotTypeName &&
               Kind == other.Kind &&
               CollectionKind == other.CollectionKind &&
               NestedPersistenceTypeName == other.NestedPersistenceTypeName &&
               IsNullable == other.IsNullable &&
               IsString == other.IsString;
    }

    public override bool Equals(object? obj)
        => obj is PersistenceMemberModel other && Equals(other);

    public override int GetHashCode()
    {
        var hash = Order;
        hash = (hash * 397) ^ MemberName.GetHashCode();
        hash = (hash * 397) ^ SnapshotMemberName.GetHashCode();
        hash = (hash * 397) ^ EntityTypeName.GetHashCode();
        hash = (hash * 397) ^ SnapshotTypeName.GetHashCode();
        hash = (hash * 397) ^ Kind.GetHashCode();
        hash = (hash * 397) ^ CollectionKind.GetHashCode();
        hash = (hash * 397) ^ (NestedPersistenceTypeName?.GetHashCode() ?? 0);
        hash = (hash * 397) ^ IsNullable.GetHashCode();
        hash = (hash * 397) ^ IsString.GetHashCode();

        return hash;
    }
}
