namespace Moongate.Http.Plugin.Data;

/// <summary>An issued API token and the moment it stops being valid.</summary>
public readonly record struct ApiTokenResult(string Token, DateTimeOffset ExpiresAt);
