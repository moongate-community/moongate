namespace Moongate.Api.Types.Protocol;

/// <summary>Identifies a version-one API envelope; zero is never valid on the wire.</summary>
public enum ApiMessageKind : byte
{
    /// <summary>An invalid or unspecified message kind.</summary>
    Unknown = 0,
    /// <summary>A call to a registered operation.</summary>
    Request = 1,
    /// <summary>A successful typed response.</summary>
    Response = 2,
    /// <summary>A protocol-defined operation error.</summary>
    Error = 3
}
