namespace Moongate.Http.Plugin.Data.Api.ServerInfo;

/// <summary>The full settings view returned to staff (identical fields to the public info minus the shard name).</summary>
public sealed record ServerSettingsResponse(
    string? Description,
    ServerContactsResponse Contacts,
    bool RegistrationEnabled,
    IReadOnlyDictionary<string, string> Assets
);
