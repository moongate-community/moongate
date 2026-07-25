namespace Moongate.Http.Plugin.Data.Api.Registration;

/// <summary>Identifies a pending public registration for a verification-message resend.</summary>
public sealed record ResendVerificationRequest(string Username, string Email);
