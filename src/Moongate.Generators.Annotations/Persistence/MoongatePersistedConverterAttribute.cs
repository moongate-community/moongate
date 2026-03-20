namespace Moongate.Generators.Annotations.Persistence;

/// <summary>
/// Overrides the built-in conversion path for a persisted member.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class MoongatePersistedConverterAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MoongatePersistedConverterAttribute"/> class.
    /// </summary>
    /// <param name="converterType">Converter type implementing custom mapping logic.</param>
    public MoongatePersistedConverterAttribute(Type converterType)
    {
        ArgumentNullException.ThrowIfNull(converterType);

        ConverterType = converterType;
    }

    /// <summary>
    /// Gets the converter type.
    /// </summary>
    public Type ConverterType { get; }
}
