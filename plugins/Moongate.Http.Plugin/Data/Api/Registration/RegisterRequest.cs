namespace Moongate.Http.Plugin.Data.Api.Registration;

/// <summary>A web self-registration request.</summary>
public sealed record RegisterRequest(string Username, string Password, string Email);
