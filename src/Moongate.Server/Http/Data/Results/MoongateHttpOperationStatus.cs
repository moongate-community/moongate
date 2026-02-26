namespace Moongate.Server.Http.Data.Results;

/// <summary>
/// Represents high-level operation outcomes for HTTP facade calls.
/// </summary>
public enum MoongateHttpOperationStatus
{
    Ok = 0,
    Created = 1,
    NoContent = 2,
    BadRequest = 3,
    Unauthorized = 4,
    NotFound = 5,
    Conflict = 6,
    ServiceUnavailable = 7
}
