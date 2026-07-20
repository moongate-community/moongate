using Moongate.Console.Admin.Plugin.Services.Console;
using Moongate.Console.Admin.Plugin.Types;
using Moongate.Core.Types;
using Moongate.Network.Types;
using Moongate.Server.Abstractions.Data;

namespace Moongate.Tests.Console;

public class ConsoleAuthTests
{
    private static AccountAuthResult Ok() => AccountAuthResult.Ok("gm");
    private static AccountAuthResult Denied() => AccountAuthResult.Denied(LoginDeniedReasonType.IncorrectCredentials);

    [Fact]
    public void Evaluate_SuccessAtOrAboveMinLevel_IsAllowed()
        => Assert.Equal(
            ConsoleAuthResultType.Allowed,
            ConsoleAuth.Evaluate(Ok(), AccountLevelType.GrandMaster, AccountLevelType.GrandMaster));

    [Fact]
    public void Evaluate_SuccessBelowMinLevel_IsInsufficientPrivileges()
        => Assert.Equal(
            ConsoleAuthResultType.InsufficientPrivileges,
            ConsoleAuth.Evaluate(Ok(), AccountLevelType.Player, AccountLevelType.GrandMaster));

    [Fact]
    public void Evaluate_AuthFailure_IsLoginFailed()
        => Assert.Equal(
            ConsoleAuthResultType.LoginFailed,
            ConsoleAuth.Evaluate(Denied(), null, AccountLevelType.GrandMaster));

    [Fact]
    public void Evaluate_SuccessButUnknownAccount_IsLoginFailed()
        => Assert.Equal(
            ConsoleAuthResultType.LoginFailed,
            ConsoleAuth.Evaluate(Ok(), null, AccountLevelType.GrandMaster));
}
