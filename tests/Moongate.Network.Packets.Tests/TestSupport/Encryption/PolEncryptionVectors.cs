using System.Text.Json;

namespace Moongate.Network.Packets.Tests.TestSupport.Encryption;

internal static class PolEncryptionVectors
{
    internal static IEnumerable<JsonElement> Read()
    {
        using var stream = typeof(PolEncryptionVectors).Assembly.GetManifestResourceStream("pol-vectors.json")!;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.GetProperty("Vectors").EnumerateArray().Select(x => x.Clone()).ToArray();
    }

    internal static byte[] Input(int length)
    {
        return Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
    }
}
