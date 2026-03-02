namespace Moongate.Server.Http.Data;

/// <summary>
/// Request payload for executing a console command through HTTP.
/// </summary>
public sealed class MoongateHttpExecuteCommandRequest
{
    public required string Command { get; init; }
}
