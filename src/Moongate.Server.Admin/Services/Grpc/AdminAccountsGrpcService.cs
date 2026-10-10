using System.Globalization;
using System.Security.Claims;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Moongate.Admin.Contracts.V1;
using Moongate.Server.Admin.Internal;
using Moongate.Server.Ultima.Interfaces;
using DomainAccountType = Moongate.Server.Core.Types.Accounts.AccountType;

namespace Moongate.Server.Admin.Services.Grpc;

public sealed class AdminAccountsGrpcService : AdminAccounts.AdminAccountsBase
{
    private readonly IAccountService _accounts;
    private readonly IAccountAdminAccessService _authority;

    public AdminAccountsGrpcService(IAccountService accounts, IAccountAdminAccessService authority)
    {
        _accounts = accounts;
        _authority = authority;
    }

    public override async Task<Empty> ChangeAccountPassword(ChangeAccountPasswordRequest request, ServerCallContext context)
    {
        if (request.AccountId == 0)
        {
            throw new RpcException(new(StatusCode.InvalidArgument, "Account ID must be nonzero."));
        }

        AdminAccountMapper.ValidatePassword(request.NewPassword);
        context.GetHttpContext().Items["AdminTargetId"] = request.AccountId;

        // Changing a password revokes the sessions of the account, as every security change does.
        // Changing your own asks for the current one too: a stolen token alone does not take the account.
        if (IsCaller(context.GetHttpContext(), request.AccountId))
        {
            if (string.IsNullOrEmpty(request.CurrentPassword))
            {
                throw new RpcException(new(StatusCode.InvalidArgument, "The current password is required to change your own."));
            }

            await _authority.ChangeOwnPasswordAsync(
                new(request.AccountId),
                request.CurrentPassword,
                request.NewPassword,
                context.CancellationToken
            );

            return new();
        }

        await _authority.ChangePasswordAsync(new(request.AccountId), request.NewPassword, context.CancellationToken);

        return new();
    }

    public override async Task<AccountSummary> UpdateAccountAccess(UpdateAccountAccessRequest request, ServerCallContext context)
    {
        if (request.AccountId == 0)
        {
            throw new RpcException(new(StatusCode.InvalidArgument, "Account ID must be nonzero."));
        }

        if (!request.HasAccountType && !request.HasCanAccessApi && !request.HasIsLocked)
        {
            throw new RpcException(new(StatusCode.InvalidArgument, "At least one setting must be sent."));
        }

        var patch = AdminAccountMapper.ToAccessPatch(request);
        var http = context.GetHttpContext();
        http.Items["AdminTargetId"] = request.AccountId;

        // Nobody locks itself out: the account of the caller keeps its lock, its API access and its type.
        if (IsCaller(http, request.AccountId) &&
            (patch.IsLocked == true ||
             patch.CanAccessApi == false ||
             patch.AccountType is { } type && type != DomainAccountType.Administrator))
        {
            throw new RpcException(new(StatusCode.FailedPrecondition, "An administrator cannot lock out its own account."));
        }

        var account = await _authority.PatchAccessAsync(new(request.AccountId), patch, context.CancellationToken);

        return AdminAccountMapper.ToSummary(account);
    }

    public override async Task<AccountSummary> CreateAccount(CreateAccountRequest request, ServerCallContext context)
    {
        var result = await _accounts.CreateAccountAsync(
            AdminAccountMapper.ToCreateOptions(request),
            context.CancellationToken
        );
        var response = AdminAccountMapper.ToCreateResponse(result);
        context.GetHttpContext().Items["AdminTargetId"] = response.AccountId;

        return response;
    }

    public override async Task<ListAccountsResponse> ListAccounts(ListAccountsRequest request, ServerCallContext context)
    {
        if (request.PageSize > 200)
        {
            throw new RpcException(new(StatusCode.InvalidArgument, "Page size cannot exceed 200."));
        }

        var page = await _accounts.ListAccountsPageAsync(
            new(request.AfterAccountId),
            request.PageSize == 0 ? 50 : (int)request.PageSize,
            context.CancellationToken
        );
        var response = new ListAccountsResponse { NextAfterAccountId = page.NextAfterId.Value };
        response.Accounts.AddRange(page.Items.Select(AdminAccountMapper.ToSummary));

        return response;
    }

    private static bool IsCaller(HttpContext http, uint accountId)
    {
        return http.User.FindFirstValue(ClaimTypes.NameIdentifier) == accountId.ToString(CultureInfo.InvariantCulture);
    }
}
