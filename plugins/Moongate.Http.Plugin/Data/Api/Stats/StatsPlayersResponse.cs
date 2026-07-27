namespace Moongate.Http.Plugin.Data.Api.Stats;

/// <summary>Who is connected: players in the world, and every open connection including the login screen.</summary>
public sealed record StatsPlayersResponse(int Online, int Connections);
