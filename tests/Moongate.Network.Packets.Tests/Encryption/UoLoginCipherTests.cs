using System.Security.Cryptography;
using Moongate.Network.Packets.Data.Encryption;
using Moongate.Network.Packets.Encryption;
using Moongate.Network.Packets.Tests.TestSupport.Encryption;

namespace Moongate.Network.Packets.Tests.Encryption;

public sealed class UoLoginCipherTests
{
    [Fact]
    public void Transform_AllPolLoginVariants_MatchIndependentVectorsAcrossCalls()
    {
        foreach (var vector in PolEncryptionVectors.Read())
        {
            var profile = UoEncryptionProfile.Parse(vector.GetProperty("Version").GetString()!);
            foreach (var hash in vector.GetProperty("loginHashes").EnumerateObject())
            {
                var length = int.Parse(hash.Name);
                foreach (var chunk in new[] { 1, 113, length })
                {
                    var cipher = new UoLoginCipher(vector.GetProperty("Seed").GetUInt32(), profile);
                    var data = PolEncryptionVectors.Input(length);
                    cipher.Transform([]);
                    for (var i = 0; i < data.Length; i += chunk)
                    {
                        cipher.Transform(data.AsSpan(i, Math.Min(chunk, data.Length - i)));
                    }

                    Assert.Equal(hash.Value.GetString(), Convert.ToHexString(SHA256.HashData(data)));
                }
            }
        }
    }
}
