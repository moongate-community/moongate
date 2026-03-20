namespace Moongate.Generators.Annotations.Persistence;

/// <summary>
/// Marks a type as eligible for generated snapshot contracts.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class MoongatePersistedEntityAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MoongatePersistedEntityAttribute"/> class for nested snapshot-only contracts.
    /// </summary>
    public MoongatePersistedEntityAttribute() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MoongatePersistedEntityAttribute"/> class for root persisted entities.
    /// </summary>
    /// <param name="typeId">Stable persistence type id.</param>
    /// <param name="typeName">Stable persistence type name.</param>
    /// <param name="schemaVersion">Schema version for generated snapshot contracts.</param>
    /// <param name="keyType">Entity key type.</param>
    public MoongatePersistedEntityAttribute(ushort typeId, string typeName, int schemaVersion, Type keyType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(keyType);

        TypeId = typeId;
        TypeName = typeName;
        SchemaVersion = schemaVersion;
        KeyType = keyType;
    }

    /// <summary>
    /// Gets the stable persistence type id for root persisted entities.
    /// </summary>
    public ushort? TypeId { get; }

    /// <summary>
    /// Gets the stable persistence type name for root persisted entities.
    /// </summary>
    public string? TypeName { get; }

    /// <summary>
    /// Gets the schema version for root persisted entities.
    /// </summary>
    public int? SchemaVersion { get; }

    /// <summary>
    /// Gets the entity key type for root persisted entities.
    /// </summary>
    public Type? KeyType { get; }
}
