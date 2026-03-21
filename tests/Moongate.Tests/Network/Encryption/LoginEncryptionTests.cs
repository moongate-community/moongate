using Moongate.Network.Encryption;

namespace Moongate.Tests.Network.Encryption;

public sealed class LoginEncryptionTests
{
    [Test]
    public void GetKeys_WhenVersionIsKnown_ShouldReturnNonZeroKeys()
    {
        var keys = LoginKeys.GetKeys(7, 0, 114);

        Assert.Multiple(
            () =>
            {
                Assert.That(keys.Key1, Is.Not.EqualTo(0u));
                Assert.That(keys.Key2, Is.Not.EqualTo(0u));
            }
        );
    }

    [Test]
    public void TryDecrypt_WhenEncryptedLoginPacketMatchesVersion_ShouldSucceed()
    {
        var seed = 0x12345678u;
        var payload = BuildPlainLoginPacket();
        var encrypted = payload.ToArray();
        var encryptor = new LoginEncryption(seed, LoginKeys.GetKeys(7, 0, 114));
        encryptor.ClientDecrypt(encrypted);

        var result = LoginEncryption.TryDecrypt(7, 0, 114, seed, encrypted, out var encryption);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(encryption, Is.Not.Null);
            }
        );
    }

    [Test]
    public void ServerEncrypt_ShouldLeavePayloadUnchanged()
    {
        var buffer = new byte[] { 0x80, 0x01, 0x02 };
        var encryption = new LoginEncryption(0x12345678u, LoginKeys.GetKeys(7, 0, 114));

        encryption.ServerEncrypt(buffer);

        Assert.That(buffer, Is.EqualTo(new byte[] { 0x80, 0x01, 0x02 }));
    }

    private static byte[] BuildPlainLoginPacket()
    {
        var payload = new byte[62];
        payload[0] = 0x80;
        payload[30] = 0x00;
        payload[60] = 0x00;
        payload[61] = 0x5D;
        return payload;
    }
}
