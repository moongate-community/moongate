namespace Moongate.Network.Exceptions;

/// <summary>
/// Raised when the byte stream cannot be framed as a UO packet (unknown packet id
/// or malformed variable length). The session layer treats it as a protocol
/// violation: log a warning and disconnect — never crash the loop.
/// </summary>
public sealed class UoFramingException : Exception
{
    public UoFramingException(string message) : base(message) { }
}
