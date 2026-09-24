using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;
using Moongate.Server.Core.Interfaces.Admin;
using DomainServerMode = Moongate.Server.Core.Types.Hosting.ServerMode;

namespace Moongate.Server.Admin.Services.Grpc;

public sealed class AdminServerGrpcService : AdminServer.AdminServerBase
{
    private readonly IAdminServerInfoProvider _info;
    public AdminServerGrpcService(IAdminServerInfoProvider info) { _info = info; }

    public override Task<GetServerInfoResponse> GetServerInfo(Empty request, ServerCallContext context)
    {
        var info = _info.GetSnapshot();
        return Task.FromResult(new GetServerInfoResponse
        {
            Version = info.Version, Codename = info.Codename, InstanceId = info.InstanceId, RealmId = info.RealmId ?? "",
            UptimeSeconds = (ulong)Math.Max(0, info.Uptime.TotalSeconds),
            Mode = info.Mode switch
            {
                DomainServerMode.Login => ServerMode.Login,
                DomainServerMode.Game => ServerMode.Game,
                DomainServerMode.Standalone => ServerMode.Standalone,
                _ => ServerMode.Unspecified
            }
        });
    }
}
