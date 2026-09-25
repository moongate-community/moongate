using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Internal;

internal static class PacketMetadata
{
    public static PacketDescriptor Create(Type packetType)
    {
        if (!packetType.IsClass ||
            packetType.IsAbstract ||
            packetType.ContainsGenericParameters ||
            !typeof(IPacket).IsAssignableFrom(packetType))
        {
            throw Error(packetType, "must be a closed concrete IPacket class");
        }

        var attribute = packetType.GetCustomAttributes(typeof(PacketHandlerAttribute), false)
                                  .Cast<PacketHandlerAttribute>()
                                  .SingleOrDefault() ??
                        throw Error(packetType, "is missing PacketHandlerAttribute");
        var incomingContract = packetType.GetInterfaces()
                                         .Any(
                                             candidate => candidate.IsGenericType &&
                                                          candidate.GetGenericTypeDefinition() ==
                                                          typeof(IIncomingPacket<>) &&
                                                          candidate.GenericTypeArguments[0] == packetType
                                         );
        var outgoingContract = typeof(IOutgoingPacket).IsAssignableFrom(packetType);
        var direction = (incomingContract, outgoingContract) switch
        {
            (true, true)  => PacketDirection.Both,
            (true, false) => PacketDirection.Incoming,
            (false, true) => PacketDirection.Outgoing,
            _             => throw Error(packetType, "has no supported incoming or outgoing direction contract")
        };

        return attribute.Sizing switch
        {
            PacketSizing.Fixed when attribute.Length is >= 1 and <= ushort.MaxValue
                => new(
                    attribute.OpCode,
                    attribute.Sizing,
                    attribute.Length,
                    attribute.Length,
                    direction,
                    packetType,
                    attribute.Description
                ),
            PacketSizing.Fixed => throw Error(packetType, "has an invalid fixed Length; expected 1..65535"),
            PacketSizing.Variable when attribute.Length == -1 && attribute.MinimumLength is >= 3 and <= ushort.MaxValue
                => new(
                    attribute.OpCode,
                    attribute.Sizing,
                    null,
                    attribute.MinimumLength,
                    direction,
                    packetType,
                    attribute.Description
                ),
            PacketSizing.Variable => throw Error(
                                         packetType,
                                         "has invalid variable sizing; Length must be -1 and MinimumLength must be 3..65535"
                                     ),
            _ => throw Error(packetType, $"has unknown packet sizing value {(int)attribute.Sizing}")
        };
    }

    private static InvalidOperationException Error(Type packetType, string message)
    {
        return new($"Packet type '{packetType.FullName}' {message}.");
    }
}
