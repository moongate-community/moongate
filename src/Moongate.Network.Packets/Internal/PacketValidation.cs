using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Internal;

internal static class PacketValidation
{
    public static bool HasFixedHeader(ReadOnlySpan<byte> data, byte opCode, int expectedLength)
    {
        return data.Length == expectedLength && !data.IsEmpty && data[0] == opCode;
    }

    public static bool HasValidHeader(ReadOnlySpan<byte> data, PacketDescriptor descriptor)
    {
        return descriptor.Sizing switch
        {
            PacketSizing.Fixed when descriptor.FixedLength is int length => HasFixedHeader(data, descriptor.OpCode, length),
            PacketSizing.Variable => HasVariableHeader(data, descriptor.OpCode, descriptor.MinimumLength),
            _ => false
        };
    }

    public static bool HasVariableHeader(ReadOnlySpan<byte> data, byte opCode, int minimumLength)
    {
        return data.Length >= minimumLength &&
               data[0] == opCode &&
               BinaryPrimitives.ReadUInt16BigEndian(data[1..3]) == data.Length;
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

    public static void ValidateFixedAscii(string value, int maximumLength, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (value.Length > maximumLength)
        {
            throw new ArgumentException($"The value must fit in {maximumLength} ASCII bytes.", parameterName);
        }

        ValidateAscii(value, parameterName);
    }
}
