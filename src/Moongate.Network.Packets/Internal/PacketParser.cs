using Moongate.Network.Packets.Interfaces;

namespace Moongate.Network.Packets.Internal;

internal delegate bool PacketParser(ReadOnlySpan<byte> data, out IPacket? packet);
