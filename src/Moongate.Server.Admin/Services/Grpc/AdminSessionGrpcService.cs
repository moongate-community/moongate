using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;
using Moongate.Server.Admin.Internal;
using Moongate.Server.Core.Interfaces.Admin;

namespace Moongate.Server.Admin.Services.Grpc;

public sealed class AdminSessionGrpcService : AdminSession.AdminSessionBase
{
    private readonly IAdminSessionStore _sessions;

    public AdminSessionGrpcService(IAdminSessionStore sessions)
    {
        _sessions = sessions;
    }

    public override async Task<Empty> Logout(Empty request, ServerCallContext context)
    {
        if (!AdminToken.TryGetDigest(context.GetHttpContext().Request.Headers, out var digest))
        {
            throw new RpcException(new(StatusCode.Unauthenticated, "Invalid administration credentials."));
        }
        var session = await _sessions.FindAsync(digest, context.CancellationToken);

        if (session is not null)
        {
            context.GetHttpContext().Items["AdminActorId"] = session.Identity.AccountId.Value;
            context.GetHttpContext().Items["AdminTargetId"] = session.Identity.AccountId.Value;
        }
        await _sessions.RemoveAsync(digest, context.CancellationToken);

        return new();
    }
}
