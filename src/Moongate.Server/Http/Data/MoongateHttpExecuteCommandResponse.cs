namespace Moongate.Server.Http.Data;

/// <summary>
/// Response payload returned by the HTTP command execution endpoint.
/// </summary>
public sealed class MoongateHttpExecuteCommandResponse
{
    public required bool Success { get; init; }

    public required string Command { get; init; }

    public required IReadOnlyList<string> OutputLines { get; init; }

    public required long Timestamp { get; init; }
}
