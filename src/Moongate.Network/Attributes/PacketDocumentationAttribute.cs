using Moongate.Network.Types;

namespace Moongate.Network.Attributes;

/// <summary>
/// Documentation metadata for a packet record: assigns it to a
/// <see cref="PacketFamilyType" /> so the docs generator
/// (<c>scripts/generate-packet-docs.cs</c>) can group the reference pages.
/// Purely informational at runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class PacketDocumentationAttribute : Attribute
{
    public PacketFamilyType Family { get; }

    public PacketDocumentationAttribute(PacketFamilyType family)
    {
        Family = family;
    }
}
