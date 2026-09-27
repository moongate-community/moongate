using System.Security.Cryptography;
using Moongate.Network.Packets.Data.Encryption;
using Moongate.Network.Packets.Encryption;
using Moongate.Network.Packets.Tests.TestSupport.Encryption;
using Moongate.Network.Packets.Types.Encryption;

namespace Moongate.Network.Packets.Tests.Encryption;

public sealed class UoGameCipherTests
{
    [Fact]
    public void Decrypt_PolGameStreams_MatchThroughRefreshAndMultipleTableRollovers()
    {
        foreach (var vector in PolEncryptionVectors.Read())
        {
            var profile = UoEncryptionProfile.Parse(vector.GetProperty("Version").GetString()!);
            foreach (var hash in vector.GetProperty("gameHashes").EnumerateObject())
            {
                var length = int.Parse(hash.Name);
                foreach (var chunk in new[] { 1, 113, length })
                {
                    var cipher = new UoGameCipher(vector.GetProperty("Seed").GetUInt32(), profile.Type);
                    var data = PolEncryptionVectors.Input(length);
                    cipher.Decrypt([]);
                    for (var i = 0; i < data.Length; i += chunk)
                    {
                        cipher.Decrypt(data.AsSpan(i, Math.Min(chunk, data.Length - i)));
                    }
                    Assert.Equal(hash.Value.GetString(), Convert.ToHexString(SHA256.HashData(data)));
                }
            }
        }
    }

    [Fact]
    public void Encrypt_OutgoingPolStream_IsIndependentFromIncomingProgress()
    {
        foreach (var vector in PolEncryptionVectors.Read())
        {
            var profile = UoEncryptionProfile.Parse(vector.GetProperty("Version").GetString()!);
            var cipher = new UoGameCipher(vector.GetProperty("Seed").GetUInt32(), profile.Type);
            var receive = PolEncryptionVectors.Input(50000);
            cipher.Decrypt(receive);
            var send = PolEncryptionVectors.Input(50000);
            for (var i = 0; i < send.Length; i += 13)
            {
                cipher.Encrypt([]);
                cipher.Encrypt(send.AsSpan(i, Math.Min(13, send.Length - i)));
            }
            Assert.Equal(vector.GetProperty("sendHashes").GetProperty("50000").GetString(), Convert.ToHexString(SHA256.HashData(send)));
        }
    }

    [Fact]
    public void Decrypt_IndependentPolGameLoginPackets_RecoverCredentialsAndSeed()
    {
        foreach (var vector in PolEncryptionVectors.Read())
        {
            var profile = UoEncryptionProfile.Parse(vector.GetProperty("Version").GetString()!);
            var cipher = new UoGameCipher(vector.GetProperty("Seed").GetUInt32(), profile.Type);
            var packet = Convert.FromHexString(vector.GetProperty("GameLogin").GetString()!);
            cipher.Decrypt(packet);
            Assert.Equal(0x91, packet[0]);
            Assert.Equal("fixture", System.Text.Encoding.ASCII.GetString(packet, 5, 7));
            Assert.Equal("example", System.Text.Encoding.ASCII.GetString(packet, 35, 7));
            Assert.Equal(vector.GetProperty("Seed").GetUInt32(), System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(1)));
        }
    }

    [Fact]
    public void Constructor_InvalidCipherTypeFails()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UoGameCipher(1, (UoEncryptionType)100));
    }
}
