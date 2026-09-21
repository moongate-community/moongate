namespace Moongate.Api.Exceptions;

/// <summary>Indicates malformed or unsupported API wire data.</summary>
public sealed class ApiProtocolException : IOException
{
    /// <summary>Creates an exception for invalid protocol data.</summary>
    public ApiProtocolException() : base("Invalid API protocol data.")
    {
    }

    /// <summary>Creates an exception with a safe diagnostic message.</summary>
    public ApiProtocolException(string message) : base(message)
    {
    }

    /// <summary>Creates an exception retaining its local cause.</summary>
    public ApiProtocolException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
