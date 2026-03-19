namespace Moongate.Server.Data.Internal.Scripting;

internal sealed record AsyncLuaJobResult(
    string JobName,
    string RequestId,
    IReadOnlyDictionary<string, object?> Payload
);
