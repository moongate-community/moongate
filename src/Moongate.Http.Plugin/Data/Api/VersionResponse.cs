namespace Moongate.Http.Plugin.Data.Api;

/// <summary>What the shard calls itself and which build it runs.</summary>
public sealed record VersionResponse(string ShardName, string Version);
