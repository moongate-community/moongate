namespace Moongate.Server.PacketHandlers.Generators.Data.Internal;

internal sealed class PersistenceEntityModel : IEquatable<PersistenceEntityModel>
{
    public PersistenceEntityModel(
        string namespaceName,
        string entityTypeName,
        string entityFullTypeName,
        bool emitsRootMetadata,
        string? keyTypeName,
        string? keyMemberName,
        ushort? typeId,
        string? typeName,
        int? schemaVersion,
        IReadOnlyList<PersistenceMemberModel> members
    )
    {
        NamespaceName = namespaceName;
        EntityTypeName = entityTypeName;
        EntityFullTypeName = entityFullTypeName;
        EmitsRootMetadata = emitsRootMetadata;
        KeyTypeName = keyTypeName;
        KeyMemberName = keyMemberName;
        TypeId = typeId;
        TypeName = typeName;
        SchemaVersion = schemaVersion;
        Members = members;
    }

    public string NamespaceName { get; }

    public string EntityTypeName { get; }

    public string EntityFullTypeName { get; }

    public bool EmitsRootMetadata { get; }

    public string? KeyTypeName { get; }

    public string? KeyMemberName { get; }

    public ushort? TypeId { get; }

    public string? TypeName { get; }

    public int? SchemaVersion { get; }

    public IReadOnlyList<PersistenceMemberModel> Members { get; }

    public bool Equals(PersistenceEntityModel? other)
    {
        if (other is null)
        {
            return false;
        }

        return NamespaceName == other.NamespaceName &&
               EntityTypeName == other.EntityTypeName &&
               EntityFullTypeName == other.EntityFullTypeName &&
               EmitsRootMetadata == other.EmitsRootMetadata &&
               KeyTypeName == other.KeyTypeName &&
               KeyMemberName == other.KeyMemberName &&
               TypeId == other.TypeId &&
               TypeName == other.TypeName &&
               SchemaVersion == other.SchemaVersion &&
               Members.SequenceEqual(other.Members);
    }

    public override bool Equals(object? obj)
        => obj is PersistenceEntityModel other && Equals(other);

    public override int GetHashCode()
    {
        var hash = NamespaceName.GetHashCode();
        hash = (hash * 397) ^ EntityTypeName.GetHashCode();
        hash = (hash * 397) ^ EntityFullTypeName.GetHashCode();
        hash = (hash * 397) ^ EmitsRootMetadata.GetHashCode();
        hash = (hash * 397) ^ (KeyTypeName?.GetHashCode() ?? 0);
        hash = (hash * 397) ^ (KeyMemberName?.GetHashCode() ?? 0);
        hash = (hash * 397) ^ (TypeId?.GetHashCode() ?? 0);
        hash = (hash * 397) ^ (TypeName?.GetHashCode() ?? 0);
        hash = (hash * 397) ^ (SchemaVersion ?? 0);

        foreach (var member in Members)
        {
            hash = (hash * 397) ^ member.GetHashCode();
        }

        return hash;
    }
}
