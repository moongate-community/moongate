namespace Moongate.Server.Data.Api;

/// <summary>A staff-level snapshot of the shard. No uptime: there is no "shard started at" to read.</summary>
public sealed record AdminStatusResponse(string ShardName, string Version, int OnlineSessions);
