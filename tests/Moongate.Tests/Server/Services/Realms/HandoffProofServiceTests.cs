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

        var proof = service.Sign(credentialKey, "realm-a", 0x12345678);

        Assert.Equal(32, proof.Length);
        Assert.True(service.Verify("alice", "password", "realm-a", 0x12345678, proof));
        Assert.False(service.Verify("alice", "password", "realm-b", 0x12345678, proof));
        Assert.False(service.Verify("alice", "password", "realm-a", 0x12345679, proof));
        Assert.False(service.Verify("alice", "bad-password", "realm-a", 0x12345678, proof));
        Assert.False(service.Verify("Alice", "password", "realm-a", 0x12345678, proof));
    }

    [Fact]
    public void Verify_RejectsMalformedProof()
    {
        using var service = new HandoffProofService(Secret);

        Assert.False(service.Verify("alice", "password", "realm-a", 1, []));
        Assert.False(service.Verify("alice", "password", "realm-a", 1, new byte[31]));
        Assert.False(service.Verify("", "password", "realm-a", 1, new byte[32]));
        Assert.False(service.Verify("alice", "password", "", 1, new byte[32]));
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
}
