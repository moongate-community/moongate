namespace Moongate.Generators.Annotations.Persistence;

/// <summary>
/// Marks a field or property as part of the generated persistence contract.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class MoongatePersistedMemberAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MoongatePersistedMemberAttribute"/> class.
    /// </summary>
    /// <param name="order">Stable snapshot member order.</param>
    public MoongatePersistedMemberAttribute(int order)
    {
        Order = order;
    }

    /// <summary>
    /// Gets the member order within the generated snapshot.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Gets or sets an optional snapshot member name override.
    /// </summary>
    public string? SnapshotName { get; set; }
}
