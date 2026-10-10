using Grpc.Core;
using Moongate.Admin.Contracts.V1;
using Moongate.Server.Admin.Internal;
using DomainAccountType = Moongate.Server.Core.Types.Accounts.AccountType;

namespace Moongate.Tests.Server.Admin;

public sealed class AdminAccountMapperTests
{
    [Fact]
    public void ToAccessPatch_OnlyTheFieldsSent_AreSet()
    {
        var none = AdminAccountMapper.ToAccessPatch(new());
        var some = AdminAccountMapper.ToAccessPatch(
            new() { IsLocked = false, AccountType = AccountType.GameMaster }
        );

        Assert.Equal((null, null, null), (none.IsLocked, none.CanAccessApi, none.AccountType));
        Assert.Equal((false, null, DomainAccountType.GameMaster), (some.IsLocked, some.CanAccessApi, some.AccountType));
    }

    [Theory, InlineData(AccountType.Unspecified), InlineData((AccountType)99)]
    public void ToAccessPatch_AnUnknownType_IsInvalid(AccountType type)
    {
        var error = Assert.Throws<RpcException>(() => AdminAccountMapper.ToAccessPatch(new() { AccountType = type }));

        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);
    }

    [Theory, InlineData(""), InlineData("   "), InlineData("a\0b")]
    public void ValidatePassword_BadPasswords_AreInvalid(string password)
    {
        Assert.Equal(StatusCode.InvalidArgument, Assert.Throws<RpcException>(() => AdminAccountMapper.ValidatePassword(password)).StatusCode);
    }

    [Fact]
    public void ValidatePassword_TooManyBytes_IsInvalid_AndTheLimitIsAccepted()
    {
        AdminAccountMapper.ValidatePassword(new string('x', 1024));

        Assert.Equal(StatusCode.InvalidArgument, Assert.Throws<RpcException>(() => AdminAccountMapper.ValidatePassword(new string('x', 1025))).StatusCode);
    }
}
