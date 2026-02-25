namespace Moongate.Server.Http.Data.Results;

/// <summary>
/// Encapsulates facade execution outcomes independently from ASP.NET result types.
/// </summary>
/// <typeparam name="T">Payload type.</typeparam>
public sealed class MoongateHttpOperationResult<T>
{
    private MoongateHttpOperationResult(
        MoongateHttpOperationStatus status,
        T? value = default,
        string? error = null,
        string? location = null
    )
    {
        Status = status;
        Value = value;
        Error = error;
        Location = location;
    }

    public string? Error { get; }

    public string? Location { get; }

    public MoongateHttpOperationStatus Status { get; }

    public T? Value { get; }

    public static MoongateHttpOperationResult<T> BadRequest(string? error = null)
        => new(MoongateHttpOperationStatus.BadRequest, default, error);

    public static MoongateHttpOperationResult<T> Conflict(string? error = null)
        => new(MoongateHttpOperationStatus.Conflict, default, error);

    public static MoongateHttpOperationResult<T> Created(T value, string? location = null)
        => new(MoongateHttpOperationStatus.Created, value, null, location);

    public static MoongateHttpOperationResult<T> NoContent()
        => new(MoongateHttpOperationStatus.NoContent);

    public static MoongateHttpOperationResult<T> NotFound(string? error = null)
        => new(MoongateHttpOperationStatus.NotFound, default, error);

    public static MoongateHttpOperationResult<T> Ok(T value)
        => new(MoongateHttpOperationStatus.Ok, value);

    public static MoongateHttpOperationResult<T> ServiceUnavailable(string? error = null)
        => new(MoongateHttpOperationStatus.ServiceUnavailable, default, error);

    public static MoongateHttpOperationResult<T> Unauthorized(string? error = null)
        => new(MoongateHttpOperationStatus.Unauthorized, default, error);
}
