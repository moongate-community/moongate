namespace Moongate.Http.Plugin.Data.Api.Stats;

/// <summary>The shard's public statistics, as a website or launcher reads them.</summary>
public sealed record ServerStatsResponse(
    DateTimeOffset GeneratedAt,
    long UptimeSeconds,
    StatsPlayersResponse Players,
    StatsAccountsResponse Accounts,
    StatsWorldResponse World,
    StatsContentResponse Content
);
