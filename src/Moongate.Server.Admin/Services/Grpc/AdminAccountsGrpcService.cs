using Grpc.Core;
using Moongate.Admin.Contracts.V1;
using Moongate.Server.Admin.Internal;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Admin.Services.Grpc;

public sealed class AdminAccountsGrpcService : AdminAccounts.AdminAccountsBase
{
    private readonly IAccountService _accounts;
    public AdminAccountsGrpcService(IAccountService accounts) { _accounts = accounts; }

    public override async Task<AccountSummary> CreateAccount(CreateAccountRequest request, ServerCallContext context)
    {
        var result = await _accounts.CreateAccountAsync(AdminAccountMapper.ToCreateOptions(request), context.CancellationToken);
        var response = AdminAccountMapper.ToCreateResponse(result);
        context.GetHttpContext().Items["AdminTargetId"] = response.AccountId;
        return response;
    }

    public override async Task<ListAccountsResponse> ListAccounts(ListAccountsRequest request, ServerCallContext context)
    {
        if (request.PageSize > 200) { throw new RpcException(new(StatusCode.InvalidArgument, "Page size cannot exceed 200.")); }
        var page = await _accounts.ListAccountsPageAsync(new(request.AfterAccountId), request.PageSize == 0 ? 50 : (int)request.PageSize,
            context.CancellationToken);
        var response = new ListAccountsResponse { NextAfterAccountId = page.NextAfterId.Value };
        response.Accounts.AddRange(page.Items.Select(AdminAccountMapper.ToSummary));
        return response;
    }
}
