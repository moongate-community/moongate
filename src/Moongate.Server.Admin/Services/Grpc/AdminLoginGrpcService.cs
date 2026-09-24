using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;
using Moongate.Server.Admin.Internal;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Admin.Services.Grpc;

public sealed class AdminLoginGrpcService : AdminLogin.AdminLoginBase
{
    private readonly IAccountAdminAccessService _authority;
    private readonly IAdminLoginThrottle _throttle;

    public AdminLoginGrpcService(IAccountAdminAccessService authority, IAdminLoginThrottle throttle)
    {
        _authority = authority;
        _throttle = throttle;
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        AdminAccountMapper.ValidateCredentials(request.Username, request.Password);
        var peer = context.GetHttpContext().Connection.RemoteIpAddress?.ToString();

        if (peer is null || !await _throttle.TryAcquireAsync(peer, request.Username, context.CancellationToken))
        {
            throw new RpcException(new(StatusCode.ResourceExhausted, "Login attempt limit reached."));
        }
        var login = await _authority.LoginAsync(request.Username, request.Password, context.CancellationToken);

        if (login is null)
        {
            throw new RpcException(new(StatusCode.Unauthenticated, "Invalid administration credentials."));
        }
        context.GetHttpContext().Items["AdminActorId"] = login.Account.AccountId.Value;
        context.GetHttpContext().Items["AdminTargetId"] = login.Account.AccountId.Value;

        return new()
        {
            AccessToken = login.AccessToken, ExpiresAt = Timestamp.FromDateTimeOffset(login.ExpiresAt),
            Account = AdminAccountMapper.ToSummary(login.Account)
        };
    }
}
