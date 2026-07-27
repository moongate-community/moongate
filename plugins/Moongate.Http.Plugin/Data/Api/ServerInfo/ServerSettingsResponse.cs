namespace Moongate.Http.Plugin.Data.Api.ServerInfo;

/// <summary>The full settings view returned to staff, including persisted registration state and readiness.</summary>
public sealed record ServerSettingsResponse(
    string? Description,
    string? Tagline,
    ServerContactsResponse Contacts,
    bool RegistrationEnabled,
    RegistrationReadinessResponse RegistrationReadiness,
    IReadOnlyDictionary<string, string> Assets
);
