using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Moongate.Tests.TestSupport.Network;

internal static class PolEncryptionFixture
{
    internal static IEnumerable<JsonElement> Read()
    {
        using var stream = typeof(PolEncryptionFixture).Assembly.GetManifestResourceStream("Moongate.Tests.TestSupport.Network.pol-vectors.json")!;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.GetProperty("Vectors").EnumerateArray().Select(x => x.Clone()).ToArray();
    }

    internal static byte[] EncryptModernLogin(byte[] plaintext, bool game, string version = "67.0.117.0")
    {
        var vector = Read().First(x => x.GetProperty("Version").GetString() == version);
        var knownPlaintext = PlainLogin(vector.GetProperty("Seed").GetUInt32(), game);
        var ciphertext = Convert.FromHexString(vector.GetProperty(game ? "GameLogin" : "LoginPacket").GetString()!);
        // Modern login and Twofish receive streams are XOR: derive the stream from independent POL fixtures.
        for (var i = 0; i < ciphertext.Length; i++)
        {
            ciphertext[i] ^= (byte)(knownPlaintext[i] ^ plaintext[i]);
        }
        return ciphertext;
    }

    internal static byte[] Seed(uint seed, bool versioned)
    {
        var data = new byte[versioned ? 21 : 4];
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(versioned ? 1 : 0), seed);
        if (versioned)
        {
            data[0] = 0xEF;
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(5), 67);
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(13), 117);
        }
        return data;
    }

    internal static byte[] PlainLogin(uint seed, bool game)
    {
        var data = new byte[game ? 65 : 62];
        data[0] = game ? (byte)0x91 : (byte)0x80;
        if (game)
        {
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(1), seed);
        }
        Encoding.ASCII.GetBytes("fixture").CopyTo(data, game ? 5 : 1);
        Encoding.ASCII.GetBytes("example").CopyTo(data, game ? 35 : 31);
        return data;
    }
}
