using Google.Protobuf;
using Moongate.Admin.Contracts.V1;

namespace Moongate.Tests.Server.Admin;

public class AdminContractsTests
{
    [Fact]
    public void CreateAccount_WirePresence_DistinguishesOmittedFromUnspecified()
    {
        var request = CreateAccountRequest.Parser.ParseFrom(Array.Empty<byte>());
        Assert.False(request.HasAccountType);
        request.AccountType = AccountType.Unspecified;
        var copy = CreateAccountRequest.Parser.ParseFrom(request.ToByteArray());
        Assert.True(copy.HasAccountType);
        Assert.Equal(AccountType.Unspecified, copy.AccountType);
    }

    [Fact]
    public void AccountSummary_WireFields_ContainOnlyPublicAccountInformation()
    {
        var fields = AccountSummary.Descriptor.Fields.InFieldNumberOrder();
        Assert.Equal(
            new[] { "account_id", "username", "account_type", "can_access_api", "is_locked", "created_at" },
            fields.Select(f => f.Name)
        );
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, fields.Select(f => f.FieldNumber));
        Assert.Equal(1, (int)AccountType.Regular);
        Assert.Equal(2, (int)AccountType.GameMaster);
        Assert.Equal(3, (int)AccountType.Administrator);
    }
}
