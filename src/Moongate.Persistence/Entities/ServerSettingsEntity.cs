using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;

namespace Moongate.Persistence.Entities;

public sealed class ServerSettingsEntity : ISerialIdEntity
{
    public Serial Id { get; set; }

    public string? Description { get; set; }

    public string? Tagline { get; set; }

    public ServerContacts Contacts { get; set; } = new();

    public bool RegistrationEnabled { get; set; }

    public Dictionary<string, ServerAssetMeta> Assets { get; set; } = new();
}
