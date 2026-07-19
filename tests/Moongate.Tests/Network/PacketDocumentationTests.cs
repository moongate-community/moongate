using System.Reflection;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Protocol;

namespace Moongate.Tests.Network;

public class PacketDocumentationTests
{
    private static readonly List<Type> PacketTypes = typeof(IOutgoingPacket).Assembly
                                                                            .GetTypes()
                                                                            .Where(
                                                                                t => t is
                                                                                    {
                                                                                        IsValueType: true,
                                                                                        Namespace: not null
                                                                                    } &&
                                                                                    t.Namespace.StartsWith(
                                                                                        "Moongate.Network.Packets.",
                                                                                        StringComparison.Ordinal
                                                                                    )
                                                                            )
                                                                            .ToList();

    [Fact]
    public void AllPacketTypes_AreDiscovered()
        => Assert.Equal(50, PacketTypes.Count);

    [Theory, MemberData(nameof(Packets))]
    public void DeclaredSize_MatchesThePacketLengthsTable(Type packetType)
    {
        var doc = packetType.GetCustomAttribute<PacketDocumentationAttribute>()!;
        var tableLength = PacketLengths.Get(Opcode(packetType));

        if (doc.SubCommand >= 0 || doc.IsVariableLength)
        {
            // 0xBF sub-packets travel inside variable-length framing even when
            // their own payload is fixed.
            Assert.Equal(PacketLengths.Variable, tableLength);
        }
        else
        {
            Assert.Equal((short)doc.Length, tableLength);
        }
    }

    [Theory, MemberData(nameof(Packets))]
    public void EveryPacket_DeclaresExactlyOneSizeKind(Type packetType)
    {
        var doc = packetType.GetCustomAttribute<PacketDocumentationAttribute>();

        Assert.NotNull(doc);
        Assert.True(
            (doc.Length > 0) ^ doc.IsVariableLength,
            $"{packetType.Name} must declare exactly one of Length or IsVariableLength"
        );
    }

    public static TheoryData<Type> Packets()
    {
        var data = new TheoryData<Type>();

        foreach (var type in PacketTypes)
        {
            data.Add(type);
        }

        return data;
    }

    private static byte Opcode(Type packetType)
    {
        var field = packetType.GetField("PacketId", BindingFlags.Public | BindingFlags.Static);

        if (field is not null)
        {
            return (byte)field.GetValue(null)!;
        }

        var property = packetType.GetProperty("PacketId", BindingFlags.Public | BindingFlags.Static);

        return (byte)property!.GetValue(null)!;
    }
}
