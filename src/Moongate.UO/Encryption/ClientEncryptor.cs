using Moongate.UO.Data.Version;

namespace Moongate.UO.Encryption;

public static class ClientEncryptor
{
    /// <summary>
    /// Calculates encryption keys from version information
    /// </summary>
    /// <param name="version">Version information</param>
    /// <returns>Client encryption keys</returns>
    public static ClientKeys CalculateKeys(ClientVersion version)
    {
        long key1 = (version.Major << 23) | (version.Minor << 14) | (version.Revision << 4);
        key1 ^= (version.Revision * version.Revision) << 9;
        key1 ^= version.Minor * version.Minor;
        key1 ^= (version.Minor * 11) << 24;
        key1 ^= (version.Revision * 7) << 19;
        key1 ^= 0x2c13a5fdu; // 739485181

        long key2 = (version.Major << 22) | (version.Revision << 13) | (version.Minor << 3);
        key2 ^= (version.Revision * version.Revision * 3) << 10;
        key2 ^= version.Minor * version.Minor;
        key2 ^= (version.Minor * 13) << 23;
        key2 ^= (version.Revision * 7) << 18;
        key2 ^= 0xa31d527fu; // 2736607871

        return new ClientKeys
        {
            Key1 = key1,
            Key2 = key2
        };
    }

      /// <summary>
    /// Decrypts data using client keys and seed
    /// </summary>
    /// <param name="data">Data to decrypt</param>
    /// <param name="keys">Client encryption keys</param>
    /// <param name="seed">Encryption seed</param>
    /// <returns>Decrypted data as byte array</returns>
    public static byte[] Decrypt(byte[] data, ClientKeys keys, uint seed)
    {
        long encryptionSeed = seed;
        long firstClientKey = keys.Key1;
        long  secondClientKey = keys.Key2;


        long currentKey0 = ((~encryptionSeed ^ 0x00001357u) << 16) | ((encryptionSeed ^ 0xffffaaaau) & 0x0000ffffu);
        long currentKey1 = ((encryptionSeed ^ 0x43210000u) >> 16) | ((~encryptionSeed ^ 0xabcdffffff) & 0xffff0000u);

        // Create a copy of the data to avoid modifying the original
        byte[] result = new byte[data.Length];
        Array.Copy(data, result, data.Length);

        for (int i = 0; i < result.Length; ++i)
        {
            result[i] = (byte)(currentKey0 ^ result[i]);
            long oldKey0 = currentKey0;
            long oldKey1 = currentKey1;

            currentKey0 = ((oldKey0 >> 1) | (oldKey1 << 31)) ^ secondClientKey;
            currentKey1 = (((((oldKey1 >> 1) | (oldKey0 << 31)) ^ (firstClientKey - 1)) >> 1) | (oldKey0 << 31)) ^ firstClientKey;
        }

        return result;
    }

    /// <summary>
    /// Encrypts data using client keys and seed
    /// </summary>
    /// <param name="data">Data to encrypt</param>
    /// <param name="keys">Client encryption keys</param>
    /// <param name="seed">Encryption seed</param>
    /// <returns>Encrypted data as byte array</returns>
    public static byte[] Encrypt(byte[] data, ClientKeys keys, uint seed)
    {
        uint encryptionSeed = seed;
        var firstClientKey = keys.Key1;
        var secondClientKey = keys.Key2;

        var currentKey0 =(long)((~encryptionSeed ^ 0x00001357u) << 16) | ((encryptionSeed ^ 0xffffaaaau) & 0x0000ffffu);
        var currentKey1 = ((encryptionSeed ^ 0x43210000u) >> 16) | ((~encryptionSeed ^ 0xabcdffffff) & 0xffff0000u);

        // Create a copy of the data to avoid modifying the original
        byte[] result = new byte[data.Length];
        Array.Copy(data, result, data.Length);

        for (int i = 0; i < result.Length; ++i)
        {
            result[i] = (byte)(currentKey0 ^ result[i]);
            var oldKey0 = currentKey0;

            var oldKey1 = currentKey1;
            currentKey0 = ((oldKey0 >> 1) | (oldKey1 << 31)) ^ secondClientKey;
            currentKey1 = (((((oldKey1 >> 1) | (oldKey0 << 31)) ^ (firstClientKey - 1)) >> 1) | (oldKey0 << 31)) ^ firstClientKey;
        }

        return result;
    }

}
