using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Internal;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Registry;

public sealed class PacketRegistry
{
    private readonly Dictionary<(byte OpCode, PacketDirection Direction), PacketDescriptor> _descriptors = [];
    private readonly Dictionary<byte, PacketParser> _incomingParsers = [];
    private readonly List<PacketDescriptor> _registeredPackets = [];
    private IReadOnlyList<PacketDescriptor> _snapshot = Array.Empty<PacketDescriptor>();

    public static PacketRegistry Default { get; } = PacketTable.CreateRegistry();
    public bool IsFrozen { get; private set; }
    public IReadOnlyList<PacketDescriptor> RegisteredPackets => IsFrozen ? _snapshot : CreateSnapshot();

    public PacketRegistry()
    {
    }

    /// <summary>
    /// Registers a packet using the direction inferred from its incoming and outgoing interfaces.
    /// Bidirectional packets are registered for both directions in one operation.
    /// </summary>
    /// <typeparam name="TPacket">The concrete packet type with packet metadata.</typeparam>
    public void RegisterPacket<TPacket>()
        where TPacket : class, IPacket
    {
        EnsureMutable();
        var descriptor = PacketMetadataCache<TPacket>.Descriptor;
        Register(descriptor, PacketParserCache<TPacket>.Parser);
    }

    public void RegisterIncoming<TPacket>()
        where TPacket : class, IIncomingPacket<TPacket>
    {
        EnsureMutable();
        var descriptor = PacketMetadataCache<TPacket>.Descriptor;
        if ((descriptor.Direction & PacketDirection.Incoming) == 0)
        {
            throw new InvalidOperationException($"Packet type '{typeof(TPacket).FullName}' is not incoming.");
        }

        Register(descriptor, PacketParserAdapter<TPacket>.TryParse);
    }

    public void RegisterOutgoing<TPacket>()
        where TPacket : class, IOutgoingPacket
    {
        EnsureMutable();
        var descriptor = PacketMetadataCache<TPacket>.Descriptor;
        if (descriptor.Direction == PacketDirection.Both)
        {
            throw new InvalidOperationException($"Bidirectional packet type '{typeof(TPacket).FullName}' must be registered with RegisterIncoming.");
        }

        if (descriptor.Direction != PacketDirection.Outgoing)
        {
            throw new InvalidOperationException($"Packet type '{typeof(TPacket).FullName}' is not outgoing.");
        }

        Register(descriptor, null);
    }

    public void Freeze()
    {
        if (IsFrozen)
        {
            return;
        }

        _snapshot = CreateSnapshot();
        IsFrozen = true;
    }

    public bool TryGetDescriptor(
        byte opCode,
        PacketDirection direction,
        [NotNullWhen(true)] out PacketDescriptor? descriptor)
    {
        if (direction is not (PacketDirection.Incoming or PacketDirection.Outgoing))
        {
            descriptor = null;
            return false;
        }

        return _descriptors.TryGetValue((opCode, direction), out descriptor);
    }

    public bool TryDecode(ReadOnlySpan<byte> data, [NotNullWhen(true)] out IPacket? packet)
    {
        packet = null;
        if (data.IsEmpty
            || !TryGetDescriptor(data[0], PacketDirection.Incoming, out var descriptor)
            || !PacketValidation.HasValidHeader(data, descriptor)
            || !_incomingParsers.TryGetValue(data[0], out var parser))
        {
            return false;
        }

        return parser(data, out packet);
    }

    private void Register(PacketDescriptor descriptor, PacketParser? parser)
    {
        var keys = descriptor.Direction == PacketDirection.Both
            ? new[] { (descriptor.OpCode, PacketDirection.Incoming), (descriptor.OpCode, PacketDirection.Outgoing) }
            : new[] { (descriptor.OpCode, descriptor.Direction) };
        EnsureAvailable(descriptor, keys);

        foreach (var key in keys)
        {
            _descriptors.Add(key, descriptor);
        }

        if (parser is not null)
        {
            _incomingParsers.Add(descriptor.OpCode, parser);
        }

        _registeredPackets.Add(descriptor);
    }

    private ReadOnlyCollection<PacketDescriptor> CreateSnapshot()
    {
        return Array.AsReadOnly(_registeredPackets
            .OrderBy(descriptor => descriptor.OpCode)
            .ThenBy(descriptor => descriptor.Direction)
            .ToArray());
    }

    private void EnsureAvailable(
        PacketDescriptor descriptor,
        IEnumerable<(byte OpCode, PacketDirection Direction)> keys)
    {
        if (_registeredPackets.Any(existing => existing.PacketType == descriptor.PacketType))
        {
            throw new InvalidOperationException($"Packet type '{descriptor.PacketType.FullName}' is already registered.");
        }

        foreach (var key in keys)
        {
            if (_descriptors.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException(
                    $"Packet type '{descriptor.PacketType.FullName}' conflicts with '{existing.PacketType.FullName}' for opcode 0x{key.OpCode:X2} {key.Direction}.");
            }
        }
    }

    private void EnsureMutable()
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("The packet registry is frozen and cannot be changed.");
        }
    }
}
