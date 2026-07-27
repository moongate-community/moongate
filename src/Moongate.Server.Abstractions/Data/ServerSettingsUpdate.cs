using Moongate.Persistence.Entities;

namespace Moongate.Server.Abstractions.Data;

/// <summary>A partial update of the server settings: a null field leaves that setting untouched.</summary>
public sealed class ServerSettingsUpdate
{
    public string? Description { get; set; }

    public string? Tagline { get; set; }

    public ServerContacts? Contacts { get; set; }

    public bool? RegistrationEnabled { get; set; }
}
