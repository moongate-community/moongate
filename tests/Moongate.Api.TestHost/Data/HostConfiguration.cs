namespace Moongate.Api.TestHost.Data;
internal sealed class HostConfiguration
{
    public required string Certificate { get; init; }
    public required string Root { get; init; }
    public required Dictionary<string, string> Peers { get; init; }
    public required Dictionary<string, ushort[]> Permissions { get; init; }
    public int Port { get; init; }
    public int Value { get; init; }
    public bool ReconnectAfterLoss { get; init; }
}
