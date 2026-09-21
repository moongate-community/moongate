using System.Buffers.Binary;
using Moongate.Network.Interfaces.Framing;
using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Services.Network.Framing;

public sealed class UoPacketFramer : INetFramer
{
    private const int VariableHeaderLength = 3;

    private readonly int _maxFrameLength;
    private readonly PacketRegistry _registry;

    public UoPacketFramer(PacketRegistry registry, int maxFrameLength = ushort.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFrameLength, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxFrameLength, ushort.MaxValue);

        if (!registry.IsFrozen)
        {
            throw new ArgumentException("The packet registry must be frozen before framing packets.", nameof(registry));
        }

        _registry = registry;
        _maxFrameLength = maxFrameLength;
    }

    public bool TryReadFrame(Span<byte> buffer, out int frameLength)
    {
        frameLength = 0;

        if (buffer.IsEmpty)
        {
            return false;
        }

        var opCode = buffer[0];

        if (!_registry.TryGetDescriptor(opCode, PacketDirection.Incoming, out var descriptor))
        {
            throw new InvalidDataException($"Opcode 0x{opCode:X2} is not registered as an incoming packet.");
        }

        return descriptor.Sizing switch
        {
            PacketSizing.Fixed => TryReadFixedFrame(buffer, descriptor, out frameLength),
            PacketSizing.Variable => TryReadVariableFrame(buffer, descriptor, out frameLength),
            _ => throw new InvalidDataException($"Opcode 0x{opCode:X2} has an unsupported packet sizing mode.")
        };
    }

    private bool TryReadFixedFrame(Span<byte> buffer, PacketDescriptor descriptor, out int frameLength)
    {
        var declaredLength = descriptor.FixedLength ??
                             throw new InvalidDataException($"Opcode 0x{descriptor.OpCode:X2} has no fixed packet length.");
        ValidateDeclaredLength(descriptor, declaredLength);

        if (buffer.Length < declaredLength)
        {
            frameLength = 0;

            return false;
        }

        frameLength = declaredLength;

        return true;
    }

    private bool TryReadVariableFrame(Span<byte> buffer, PacketDescriptor descriptor, out int frameLength)
    {
        if (buffer.Length < VariableHeaderLength)
        {
            frameLength = 0;

            return false;
        }

        var declaredLength = BinaryPrimitives.ReadUInt16BigEndian(buffer[1..VariableHeaderLength]);
        ValidateDeclaredLength(descriptor, declaredLength);

        if (buffer.Length < declaredLength)
        {
            frameLength = 0;

            return false;
        }

        frameLength = declaredLength;

        return true;
    }

    private void ValidateDeclaredLength(PacketDescriptor descriptor, int declaredLength)
    {
        if (declaredLength < descriptor.MinimumLength || declaredLength > _maxFrameLength)
        {
            throw new InvalidDataException(
                $"Opcode 0x{descriptor.OpCode:X2} declared invalid frame length {declaredLength}."
            );
        }
    }
}
