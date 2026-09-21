using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Account;

public class AccountCreateResult
{
    public bool Success { get; set; }

    public AccountCreateResultType ResultType { get; set; }

    public AccountEntity? Account { get; set; }

    public Exception? Exception { get; set; }

    public AccountCreateResult(
        bool success,
        AccountCreateResultType resultType,
        AccountEntity? account = null,
        Exception? exception = null
    )
    {
        Success = success;
        ResultType = resultType;
        Account = account;
        Exception = exception;
    }
}
