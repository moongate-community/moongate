using Moongate.UO.Data.Version;

namespace Moongate.UO.Encryption;

public class ClientEncryptor
{
    /// <summary>
    /// Retrieve client keys based on version info
    /// Converted from PHP version
    /// </summary>
    /// <param name="major">Major version</param>
    /// <param name="minor">Minor version</param>
    /// <param name="revision">Revision version</param>
    /// <param name="prototype">Prototype version</param>
    /// <returns>ClientKeys struct</returns>
    public static ClientKeys CalculateKeys(uint major, uint minor, uint revision, uint prototype)
    {
        var key1 = (major << 23) | (minor << 14) | (revision << 4);
        key1 ^= (revision * revision) << 9;
        key1 ^= minor * minor;
        key1 ^= (minor * 11) << 24;
        key1 ^= (revision * 7) << 19;
        key1 ^= 0x2c13a5fdu;

        var key2 = (major << 22) | (revision << 13) | (minor << 3);
        key2 ^= (revision * revision * 3) << 10;
        key2 ^= minor * minor;
        key2 ^= (minor * 13) << 23;
        key2 ^= (revision * 7) << 18;
        key2 ^= 0xa31d527fu;

        return new()
        {
            Key1 = key1,
            Key2 = key2
        };
    }

    /// <summary>
    /// Helper method to calculate keys from ClientVersion
    /// </summary>
    /// <param name="clientVersion">ClientVersion instance</param>
    /// <returns>ClientKeys struct</returns>
    public static ClientKeys CalculateKeys(ClientVersion clientVersion)
        => CalculateKeys(
            (uint)clientVersion.Major,
            (uint)clientVersion.Minor,
            (uint)clientVersion.Revision,
            (uint)clientVersion.Patch
        );

    // Legacy methods for backward compatibility with byte arrays

    /// <summary>
    /// Legacy decrypt method (byte array version)
    /// </summary>
    /// <param name="data">Data to decrypt</param>
    /// <param name="keys">Client encryption keys</param>
    /// <param name="seed">Encryption seed</param>
    /// <returns>Decrypted data as byte array</returns>
    public static byte[] Decrypt(byte[] data, ClientKeys keys, uint seed)
    {
        var encryptionSeed = seed;
        var firstClientKey = keys.Key1;
        var secondClientKey = keys.Key2;

        var currentKey0 = ((~encryptionSeed ^ 0x00001357u) << 16) | ((encryptionSeed ^ 0xffffaaaau) & 0x0000ffffu);
        var currentKey1 = ((encryptionSeed ^ 0x43210000u) >> 16) | ((~encryptionSeed ^ 0xABCDFFFFu) & 0xffff0000u);

        var result = new byte[data.Length];
        Array.Copy(data, result, data.Length);

        for (var i = 0; i < result.Length; ++i)
        {
            result[i] = (byte)(currentKey0 ^ result[i]);
            var oldKey0 = currentKey0;
            var oldKey1 = currentKey1;
            currentKey0 = ((oldKey0 >> 1) | (oldKey1 << 31)) ^ secondClientKey;
            currentKey1 = (((((oldKey1 >> 1) | (oldKey0 << 31)) ^ (firstClientKey - 1)) >> 1) | (oldKey0 << 31)) ^
                          firstClientKey;
        }

        return result;
    }

    /// <summary>
    /// Decrypts packets received from client on first connection
    /// Works with hex string arrays like the original PHP version
    /// </summary>
    /// <param name="data">Array of hex strings to decrypt</param>
    /// <param name="keys">Client encryption keys</param>
    /// <param name="seed">Encryption seed</param>
    /// <returns>Array of decrypted hex strings</returns>
    public static string[] DecryptPacket(string[] data, ClientKeys keys, uint seed)
    {
        var key1 = keys.Key1;
        var key2 = keys.Key2;

        var orgTable1 = (((~seed ^ 0x00001357u) << 16) | ((seed ^ 0xFFFFAAAAu) & 0x0000FFFFu)) & 0xFFFFFFFFu;
        var orgTable2 = (((seed ^ 0x43210000u) >> 16) | ((~seed ^ 0xABCDFFFFu) & 0xFFFF0000u)) & 0xFFFFFFFFu;

        var result = new string[data.Length];

        for (var i = 0; i < data.Length; i++)
        {
            var hexValue = Convert.ToUInt32(data[i], 16);
            var decrypted = (orgTable1 ^ hexValue) & 0xFFu;
            result[i] = decrypted.ToString("X2");

            var oldkey0 = orgTable1;
            var oldkey1 = orgTable2;
            orgTable1 = (((oldkey0 >> 1) | (oldkey1 << 31)) ^ key2) & 0xFFFFFFFFu;
            orgTable2 = ((((((oldkey1 >> 1) | (oldkey0 << 31)) ^ (key1 - 1)) >> 1) | (oldkey0 << 31)) ^ key1) & 0xFFFFFFFFu;
        }

        return result;
    }

    /// <summary>
    /// Legacy encrypt method (byte array version)
    /// </summary>
    /// <param name="data">Data to encrypt</param>
    /// <param name="keys">Client encryption keys</param>
    /// <param name="seed">Encryption seed</param>
    /// <returns>Encrypted data as byte array</returns>
    public static byte[] Encrypt(byte[] data, ClientKeys keys, uint seed)
        => Decrypt(data, keys, seed); // XOR encryption is symmetric

    /// <summary>
    /// Encrypts packets for client (reverse of decrypt operation)
    /// </summary>
    /// <param name="data">Array of hex strings to encrypt</param>
    /// <param name="keys">Client encryption keys</param>
    /// <param name="seed">Encryption seed</param>
    /// <returns>Array of encrypted hex strings</returns>
    public static string[] EncryptPacket(string[] data, ClientKeys keys, uint seed)
    {
        var key1 = keys.Key1;
        var key2 = keys.Key2;

        var orgTable1 = (((~seed ^ 0x00001357u) << 16) | ((seed ^ 0xFFFFAAAAu) & 0x0000FFFFu)) & 0xFFFFFFFFu;
        var orgTable2 = (((seed ^ 0x43210000u) >> 16) | ((~seed ^ 0xABCDFFFFu) & 0xFFFF0000u)) & 0xFFFFFFFFu;

        var result = new string[data.Length];

        for (var i = 0; i < data.Length; i++)
        {
            var hexValue = Convert.ToUInt32(data[i], 16);
            var encrypted = (orgTable1 ^ hexValue) & 0xFFu;
            result[i] = encrypted.ToString("X2");

            var oldkey0 = orgTable1;
            var oldkey1 = orgTable2;
            orgTable1 = (((oldkey0 >> 1) | (oldkey1 << 31)) ^ key2) & 0xFFFFFFFFu;
            orgTable2 = ((((((oldkey1 >> 1) | (oldkey0 << 31)) ^ (key1 - 1)) >> 1) | (oldkey0 << 31)) ^ key1) & 0xFFFFFFFFu;
        }

        return result;
    }
}
