using Moongate.Network.Types;

namespace Moongate.Network.Attributes;

/// <summary>
/// Documentation metadata for a packet record: assigns it to a
/// <see cref="PacketFamilyType" /> and declares its wire size, so the docs
/// generator (<c>scripts/generate-packet-docs.cs</c>) can group the reference
/// pages and render structured data. Exactly one of <see cref="Length" /> or
/// <see cref="IsVariableLength" /> must be set. Purely informational at
/// runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class PacketDocumentationAttribute : Attribute
{
    private const int Unset = -1;

    public PacketFamilyType Family { get; }

    /// <summary>Fixed wire size in bytes, packet id included.</summary>
    public int Length { get; set; } = Unset;

    /// <summary>True when the total length travels as a ushort inside the packet.</summary>
    public bool IsVariableLength { get; set; }

    /// <summary>
    /// The 0xBF sub-command this packet is multiplexed under, when it is one
    /// of the General Information sub-packets.
    /// </summary>
    public int SubCommand { get; set; } = Unset;

    /// <summary>
    /// Canonical UO packet name (POL packet guide), when it differs from the
    /// name derived from the class name.
    /// </summary>
    public string? Name { get; set; }

    public PacketDocumentationAttribute(PacketFamilyType family)
    {
        Family = family;
    }
}
