namespace Moongate.Http.Plugin.Data.Api.Registration;

/// <summary>Carries the single-use token that verifies an account's email.</summary>
public sealed record VerifyEmailRequest(string Token);
