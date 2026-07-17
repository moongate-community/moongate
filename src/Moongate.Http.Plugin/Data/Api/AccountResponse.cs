namespace Moongate.Http.Plugin.Data.Api;

/// <summary>
/// An account as the API reports it. Deliberately not <c>AccountEntity</c>, which carries PasswordHash
/// and ActivationToken: returning the entity would publish both to every caller, log and proxy cache on
/// the way. Naming the fields here means a field added to the entity later cannot leak by default.
/// </summary>
public sealed record AccountResponse(
    string Username,
    string? Email,
    string Level,
    bool IsActive,
    int CharacterCount
);
