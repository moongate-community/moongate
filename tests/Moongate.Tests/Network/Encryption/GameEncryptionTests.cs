using Moongate.Network.Encryption;

namespace Moongate.Tests.Network.Encryption;

public sealed class GameEncryptionTests
{
    [Test]
    public void ServerEncrypt_ShouldTransformOutgoingPayload()
    {
        var payload = Enumerable.Range(0, 32).Select(static i => (byte)i).ToArray();
        var encryption = new GameEncryption(0x12345678u);

        encryption.ServerEncrypt(payload);

        Assert.That(payload, Is.Not.EqualTo(Enumerable.Range(0, 32).Select(static i => (byte)i).ToArray()));
    }

    [Test]
    public void TryDecrypt_WhenEncryptedGameLoginPacketMatchesSeed_ShouldSucceed()
    {
        var seed = 0x12345678u;
        var payload = new byte[65];
        payload[0] = 0x91;
        var encrypted = payload.ToArray();
        var encryptor = new GameEncryption(seed);
        encryptor.ClientDecrypt(encrypted);

        var result = GameEncryption.TryDecrypt(seed, encrypted, out var encryption);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(encryption, Is.Not.Null);
            }
        );
    }
}
