namespace Moongate.Generators.Annotations.Persistence;

/// <summary>
/// Excludes a field or property from generated persistence contracts.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class MoongatePersistedIgnoreAttribute : Attribute;
