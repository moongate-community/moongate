namespace Moongate.Api.Types.Protocol;
/// <summary>Stable wire error codes. Zero and unspecified values are invalid.</summary>
public enum ApiErrorCode : byte
{
    UnsupportedOperation = 1,
    Forbidden = 2,
    InvalidRequest = 3,
    Busy = 4,
    Unavailable = 5,
    DeadlineExceeded = 6,
    InternalError = 7
}
