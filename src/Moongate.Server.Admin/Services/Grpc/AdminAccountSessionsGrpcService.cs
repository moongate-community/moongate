using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Admin.Services.Grpc;

public sealed class AdminAccountSessionsGrpcService : AdminAccountSessions.AdminAccountSessionsBase
{
    private readonly IAccountAdminAccessService _authority;
    public AdminAccountSessionsGrpcService(IAccountAdminAccessService authority) { _authority = authority; }

    public override async Task<Empty> RevokeAccountSessions(RevokeAccountSessionsRequest request, ServerCallContext context)
    {
        if (request.AccountId == 0) { throw new RpcException(new(StatusCode.InvalidArgument, "Account ID must be nonzero.")); }
        context.GetHttpContext().Items["AdminTargetId"] = request.AccountId;
        await _authority.RevokeSessionsAsync(new(request.AccountId), context.CancellationToken);
        return new();
    }
}
