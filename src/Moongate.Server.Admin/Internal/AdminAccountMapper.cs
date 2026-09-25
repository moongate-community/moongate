using System.Text;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;
using Moongate.Server.Core.Data.Admin;
using Moongate.Server.Ultima.Data.Account;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Types;
using DomainAccountType = Moongate.Server.Core.Types.Accounts.AccountType;

namespace Moongate.Server.Admin.Internal;

internal static class AdminAccountMapper
{
    public static void ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            username.Length > 255 ||
            username.Contains('\0') ||
            string.IsNullOrWhiteSpace(password) ||
            password.Contains('\0') ||
            Encoding.UTF8.GetByteCount(password) > 1024)
        {
            throw new RpcException(new(StatusCode.InvalidArgument, "Invalid account credentials format."));
        }
    }

    public static AccountCreateOptions ToCreateOptions(CreateAccountRequest request)
    {
        ValidateCredentials(request.Username, request.Password);

        return new()
        {
            Username = request.Username, Password = request.Password, CanAccessApi = request.CanAccessApi,
            AccountType = !request.HasAccountType
                              ? DomainAccountType.Regular
                              : request.AccountType switch
                              {
                                  AccountType.Regular => DomainAccountType.Regular,
                                  AccountType.GameMaster => DomainAccountType.GameMaster,
                                  AccountType.Administrator => DomainAccountType.Administrator,
                                  _ => throw new RpcException(new(StatusCode.InvalidArgument, "Unknown account type."))
                              }
        };
    }

    public static AccountSummary ToCreateResponse(AccountCreateResult result)
    {
        if (result.Success && result.Account is not null) { return ToSummary(result.Account); }

        throw new RpcException(
            result.ResultType == AccountCreateResultType.UsernameAlreadyExists
                ? new(StatusCode.AlreadyExists, "Account already exists.")
                : new(StatusCode.Unavailable, "Account storage unavailable.")
        );
    }

    public static AccountSummary ToSummary(AccountEntity account)
    {
        return ToSummary(
                new AdminAccountSnapshot(
                    account.Id,
                    account.Username,
                    account.AccountType,
                    account.CanAccessApi,
                    account.IsLocked,
                    account.CreatedAt
                )
            );
    }

    public static AccountSummary ToSummary(AdminAccountSnapshot account)
    {
        return new()
        {
            AccountId = account.AccountId.Value, Username = account.Username,
            AccountType = account.AccountType switch
            {
                DomainAccountType.Regular => AccountType.Regular,
                DomainAccountType.GameMaster => AccountType.GameMaster,
                DomainAccountType.Administrator => AccountType.Administrator,
                _ => throw new RpcException(new(StatusCode.Internal, "Invalid stored account type."))
            },
            CanAccessApi = account.CanAccessApi, IsLocked = account.IsLocked,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(account.CreatedAt, DateTimeKind.Utc))
        };
    }
}
