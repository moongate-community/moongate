namespace Moongate.Http.Plugin.Data.Api;

/// <summary>Credentials posted to the login endpoint.</summary>
public sealed record LoginRequest(string Username, string Password);
