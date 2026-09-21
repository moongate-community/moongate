using MessagePack;
using Moongate.Api.Types.Protocol;

namespace Moongate.Api.Data.Errors;

/// <summary>A bounded remote failure without payload or exception details.</summary>
[MessagePackObject]
public sealed class ApiError
{
    [Key(0)]
    public ApiErrorCode Code { get; init; }

    [Key(1)]
    public string Message { get; set; } = "";
}
