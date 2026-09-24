using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Realms;

namespace Moongate.Tests.Server.Services.Realms;

public sealed class HandoffProofServiceTests
{
    private static readonly byte[] Secret = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();

    [Fact]
    public void DeriveCredentialKey_SeparatesUsernamePasswordAndSecret()
    {
        using var service = new HandoffProofService(Secret);
        using var otherSecretService = new HandoffProofService(Enumerable.Repeat((byte)42, 32).ToArray());

        var key = service.DeriveCredentialKey("alice", "password");

        Assert.Equal(32, key.Length);
        Assert.Equal(key, service.DeriveCredentialKey("alice", "password"));
        Assert.False(key.SequenceEqual(service.DeriveCredentialKey("Alice", "password")));
        Assert.False(key.SequenceEqual(service.DeriveCredentialKey("alice", "other-password")));
        Assert.False(key.SequenceEqual(otherSecretService.DeriveCredentialKey("alice", "password")));
    }

    [Fact]
    public void Sign_BindsRealmAndRedirectKey()
    {
        using var service = new HandoffProofService(Secret);
        var credentialKey = service.DeriveCredentialKey("alice", "password");
        var handoff = Handoff();

        var proof = service.Sign(credentialKey, handoff, 0x12345678);

        Assert.Equal(32, proof.Length);
        Assert.True(service.Verify("alice", "password", handoff, 0x12345678, proof));
        Assert.False(
            service.Verify(
                "alice",
                "password",
                handoff with { RealmId = "realm-b" },
                0x12345678,
                proof
            )
        );
        Assert.False(service.Verify("alice", "password", handoff, 0x12345679, proof));
        Assert.False(service.Verify("alice", "bad-password", handoff, 0x12345678, proof));
        Assert.False(service.Verify("Alice", "password", handoff, 0x12345678, proof));
    }

    [Fact]
    public void Verify_RejectsTamperedAccountPrivilegeInstanceAndVersion()
    {
        using var service = new HandoffProofService(Secret);
        var handoff = Handoff();
        var credentialKey = service.DeriveCredentialKey("alice", "password");
        var proof = service.Sign(credentialKey, handoff, 0x12345678);

        Assert.False(
            service.Verify(
                "alice",
                "password",
                handoff with { AccountId = new(43) },
                0x12345678,
                proof
            )
        );
        Assert.False(
            service.Verify(
                "alice",
                "password",
                handoff with { AccountType = AccountType.Administrator },
                0x12345678,
                proof
            )
        );
        Assert.False(
            service.Verify(
                "alice",
                "password",
                handoff with { InstanceId = Guid.NewGuid() },
                0x12345678,
                proof
            )
        );
        Assert.False(
            service.Verify(
                "alice",
                "password",
                handoff with { ClientVersion = "7.0.118" },
                0x12345678,
                proof
            )
        );
    }

    [Fact]
    public void Verify_RejectsMalformedProof()
    {
        using var service = new HandoffProofService(Secret);

        Assert.False(service.Verify("alice", "password", Handoff(), 1, []));
        Assert.False(service.Verify("alice", "password", Handoff(), 1, new byte[31]));
        Assert.False(service.Verify("", "password", Handoff(), 1, new byte[32]));
        Assert.False(service.Verify("alice", "password", Handoff() with { RealmId = "" }, 1, new byte[32]));
    }

    [Fact]
    public void DeriveCredentialKey_DoesNotRetainCallerSecretBuffer()
    {
        var secret = Secret.ToArray();
        using var service = new HandoffProofService(secret);
        var before = service.DeriveCredentialKey("alice", "password");
        Array.Clear(secret);

        Assert.Equal(before, service.DeriveCredentialKey("alice", "password"));
    }

    private static PendingHandoff Handoff()
        => new(
            new(42),
            AccountType.Regular,
            "alice",
            "realm-a",
            Guid.Parse("2a2fc83d-eac8-4d65-b105-18154901bd3d"),
            "7.0.117"
        );
}
