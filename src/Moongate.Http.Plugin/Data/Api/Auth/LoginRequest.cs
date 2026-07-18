namespace Moongate.Http.Plugin.Data.Api.Auth;

/// <summary>Credentials posted to the login endpoint.</summary>
public sealed record LoginRequest(string Username, string Password);
