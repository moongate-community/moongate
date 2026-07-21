namespace Moongate.Http.Plugin.Data.Api.Stats;

/// <summary>The shard's user base: accounts registered, accounts verified, and characters created.</summary>
public sealed record StatsAccountsResponse(int Total, int Active, int Characters);
