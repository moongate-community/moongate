namespace Moongate.Http.Plugin.Data.Api;

/// <summary>
/// A new account. <c>Level</c> is the <c>AccountLevelType</c> name; omitted means Player, the safe
/// default — an account that gains staff rights by accident is the wrong way to fail.
/// </summary>
public sealed record CreateAccountRequest(string Username, string Password, string? Email, string? Level);
