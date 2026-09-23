using MessagePack;
using Moongate.Server.Core.Types.Realms;

namespace Moongate.Server.Core.Data.Realms.Api;

[MessagePackObject]
public sealed class RenewRealmResponse
{
    [Key(0)] public bool Accepted { get; init; }
    [Key(1)] public RealmRegistrationError Error { get; init; }
}
