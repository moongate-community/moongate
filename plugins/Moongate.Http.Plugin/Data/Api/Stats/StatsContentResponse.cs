namespace Moongate.Http.Plugin.Data.Api.Stats;

/// <summary>How much content the shard has loaded: the templates available to spawn from.</summary>
public sealed record StatsContentResponse(int ItemTemplates, int MobileTemplates);
