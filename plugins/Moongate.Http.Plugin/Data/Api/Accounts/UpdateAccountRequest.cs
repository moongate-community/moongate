namespace Moongate.Http.Plugin.Data.Api.Accounts;

/// <summary>
/// The fields to change. Every one is optional: absent means "leave alone", which is what makes this a
/// PATCH rather than a PUT. Password is write-only — it enters here and never appears in a response.
/// Email is not here: the account service has no setter for it.
/// </summary>
public sealed record UpdateAccountRequest(string? Level, bool? IsActive, string? Password);
