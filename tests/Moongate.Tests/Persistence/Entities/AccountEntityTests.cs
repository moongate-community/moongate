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
        Assert.False(account.IsPublicRegistrationPending);
    }

    [Fact]
    public void RoundTrip_CurrentVerificationState_PreservesHashedPendingRegistration()
    {
        var expiresAt = new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);
        var original = new AccountEntity
        {
            Username = "pending-account",
            PasswordHash = "password-hash",
            ActivationToken = string.Empty,
            ActivationTokenHash = new string('A', 64),
            ActivationTokenExpiresAtUtc = expiresAt,
            IsPublicRegistrationPending = true
        };

        var restored = RoundTrip<AccountEntity, AccountEntity>(original);

        Assert.Equal(original.ActivationTokenHash, restored.ActivationTokenHash);
        Assert.Equal(expiresAt, restored.ActivationTokenExpiresAtUtc);
        Assert.True(restored.IsPublicRegistrationPending);
        Assert.Empty(restored.ActivationToken);
    }

    // Mirrors the persistence layer, which registers MessagePack's contractless resolver.
    private static TOut RoundTrip<TIn, TOut>(TIn value)
        => MessagePackSerializer.Deserialize<TOut>(
            MessagePackSerializer.Serialize(value, ContractlessStandardResolver.Options),
            ContractlessStandardResolver.Options
        );
}
