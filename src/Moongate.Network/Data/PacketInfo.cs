using Moongate.Network.Protocol;
using Moongate.Network.Types;

namespace Moongate.Network.Data;

/// <summary>Metadata of one UO packet id: name, direction and wire size.</summary>
public sealed record PacketInfo
{
    public byte Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public PacketDirectionType Direction { get; init; }

    /// <summary>Fixed size in bytes, or <see cref="PacketLengths.Variable" />.</summary>
    public short Size { get; init; }
}
