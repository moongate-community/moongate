namespace Moongate.Http.Plugin.Data.Api.ServerInfo;

/// <summary>The public server profile a website or launcher reads: identity, contacts, assets, and whether registration is open.</summary>
public sealed record ServerInfoResponse(
    string ShardName,
    string? Description,
    ServerContactsResponse Contacts,
    bool RegistrationEnabled,
    IReadOnlyDictionary<string, string> Assets
);
