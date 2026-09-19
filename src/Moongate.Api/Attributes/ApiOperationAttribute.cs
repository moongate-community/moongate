namespace Moongate.Api.Attributes;

/// <summary>Assigns a stable wire identifier to an API request contract.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ApiOperationAttribute : Attribute
{
    /// <summary>Gets the nonzero wire identifier.</summary>
    public ushort Id { get; }

    /// <summary>Creates an operation identity independent of CLR names.</summary>
    public ApiOperationAttribute(ushort id)
    {
        ArgumentOutOfRangeException.ThrowIfZero(id);
        Id = id;
    }
}
