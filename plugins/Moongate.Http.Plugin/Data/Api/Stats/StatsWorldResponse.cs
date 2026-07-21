namespace Moongate.Http.Plugin.Data.Api.Stats;

/// <summary>How much the world holds: creatures nobody plays, and persisted items.</summary>
public sealed record StatsWorldResponse(int Npcs, int Items);
