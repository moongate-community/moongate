using MessagePack;
using Moongate.Api.Attributes;
using Moongate.Api.Interfaces.Contracts;

namespace Moongate.Server.Core.Data.Realms.Api;

[ApiOperation(0x0101), MessagePackObject]
public sealed class RenewRealmRequest : IApiRequest<RenewRealmResponse>
{
    [Key(0)] public string RealmId { get; set; } = "";
    [Key(1)] public byte[] LeaseId { get; set; } = [];
}
