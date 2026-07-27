namespace Moongate.Http.Plugin.Data.Api.ServerInfo;

/// <summary>A partial settings update: an omitted (null) field is left unchanged.</summary>
public sealed class UpdateServerSettingsRequest
{
    public string? Description { get; set; }

    public string? Tagline { get; set; }

    public ServerContactsResponse? Contacts { get; set; }

    public bool? RegistrationEnabled { get; set; }
}
