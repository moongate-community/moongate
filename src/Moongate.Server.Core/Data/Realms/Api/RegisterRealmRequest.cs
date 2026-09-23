using MessagePack;
using Moongate.Api.Attributes;
using Moongate.Api.Interfaces.Contracts;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Core.Data.Realms.Api;

[ApiOperation(0x0100), MessagePackObject]
public sealed class RegisterRealmRequest : IApiRequest<RegisterRealmResponse>
{
    [Key(0)] public string RealmId { get; set; } = "";
    [Key(1)] public byte[] InstanceId { get; set; } = [];
    [Key(2)] public ushort ServerIndex { get; init; }
    [Key(3)] public string Name { get; set; } = "";
    [Key(4)] public string AdvertisedAddress { get; set; } = "";
    [Key(5)] public ushort AdvertisedPort { get; init; }
    [Key(6)] public AccountType MinimumAccountType { get; init; }
}
