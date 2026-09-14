using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Moongate.Network.Packets.Internal;

internal static class PacketValidation
{
    public static bool HasFixedHeader(ReadOnlySpan<byte> data, byte opCode, int expectedLength)
    {
        return data.Length == expectedLength && !data.IsEmpty && data[0] == opCode;
    }

    public static bool HasVariableHeader(ReadOnlySpan<byte> data, byte opCode, int minimumLength)
    {
        return data.Length >= minimumLength
               && data[0] == opCode
               && BinaryPrimitives.ReadUInt16BigEndian(data[1..3]) == data.Length;
    }

    public static void ValidateFixedAscii(string value, int maximumLength, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length > maximumLength)
        {
            throw new ArgumentException($"The value must fit in {maximumLength} ASCII bytes.", parameterName);
        }

        ValidateAscii(value, parameterName);
    }

    public static void ValidateAscii(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        foreach (var character in value)
        {
            if (character is '\0' or > '\x7F')
            {
                throw new ArgumentException("The value must contain only non-NUL ASCII characters.", parameterName);
            }
        }
    }

    public static byte[] SnapshotIPv4(IPAddress address, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(address, parameterName);
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException("The address must be IPv4.", parameterName);
        }

        return address.GetAddressBytes();
    }
}
