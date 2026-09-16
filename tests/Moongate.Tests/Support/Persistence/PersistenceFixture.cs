using System.Buffers.Binary;

namespace Moongate.Tests.Support.Persistence;

public static class PersistenceFixture
{
    public static void Rechecksum(byte[] bytes, int start, int length)
    {
        uint crc = uint.MaxValue;
        foreach (var value in bytes.AsSpan(start, length))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xEDB88320);
            }
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(start + length), ~crc);
    }
}
