namespace Moongate.Server.Data.Internal.Scripting;

internal sealed record AsyncLuaJobRequest(
    string JobName,
    string RequestId,
    string? Key,
    IReadOnlyDictionary<string, object?> Payload
);
