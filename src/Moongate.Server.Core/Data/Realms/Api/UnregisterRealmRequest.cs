using MessagePack;
using Moongate.Api.Attributes;
using Moongate.Api.Interfaces.Contracts;

namespace Moongate.Server.Core.Data.Realms.Api;

[ApiOperation(0x0102), MessagePackObject]
public sealed class UnregisterRealmRequest : IApiRequest<UnregisterRealmResponse>
{
    [Key(0)] public string RealmId { get; set; } = "";
    [Key(1)] public byte[] LeaseId { get; set; } = [];
}
