namespace Moongate.Http.Plugin.Data.Api.ServerInfo;

/// <summary>The shard's public contact points.</summary>
public sealed record ServerContactsResponse(string? Website, string? Email, string? Discord);
