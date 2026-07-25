using MessagePack;
using MessagePack.Resolvers;
using Moongate.Persistence.Entities;

namespace Moongate.Tests.Persistence.Entities;

public sealed class AccountEntityTests
{
    [Fact]
    public void Deserialize_SaveWrittenBeforeVerificationTokenFieldsExisted_DefaultsTheNewFields()
    {
        var oldSave = new Dictionary<string, object>
        {
            ["Username"] = "old-account",
            ["ActivationToken"] = "legacy-token"
        };

        var account = RoundTrip<Dictionary<string, object>, AccountEntity>(oldSave);

        Assert.Equal("old-account", account.Username);
        Assert.Equal("legacy-token", account.ActivationToken);
        Assert.Empty(account.ActivationTokenHash);
        Assert.Null(account.ActivationTokenExpiresAtUtc);
    }

    // Mirrors the persistence layer, which registers MessagePack's contractless resolver.
    private static TOut RoundTrip<TIn, TOut>(TIn value)
        => MessagePackSerializer.Deserialize<TOut>(
            MessagePackSerializer.Serialize(value, ContractlessStandardResolver.Options),
            ContractlessStandardResolver.Options
        );
}
