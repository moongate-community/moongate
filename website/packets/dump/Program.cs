using System.Reflection;
using System.Text.Json;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima;

namespace Moongate.Website.Packets;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static void Main(string[] args)
    {
        // The default registry is authoritative for the built-in packets. The plugin adds
        // incoming types at runtime; reflect its attributed packet classes without starting a server.
        var builtIn = PacketRegistry.Default.RegisteredPackets.Select(ToRow);
        var ultima = typeof(MoongateUltimaPlugin).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters)
            .Where(type => type.GetCustomAttribute<PacketHandlerAttribute>() is not null)
            .Select(ToRow);
        var rows = builtIn.Concat(ultima).ToArray();
        File.WriteAllText(args[0], JsonSerializer.Serialize(rows, JsonOptions));
    }

    private static object ToRow(PacketDescriptor descriptor)
    {
        return Row(
            descriptor.PacketType,
            descriptor.OpCode,
            descriptor.Sizing,
            descriptor.FixedLength,
            descriptor.MinimumLength,
            descriptor.Direction,
            descriptor.Description
        );
    }

    private static object ToRow(Type type)
    {
        var attribute = type.GetCustomAttribute<PacketHandlerAttribute>()!;
        var incoming = type.GetInterfaces().Any(candidate =>
            candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IIncomingPacket<>) &&
            candidate.GenericTypeArguments[0] == type
        );
        var outgoing = typeof(IOutgoingPacket).IsAssignableFrom(type);
        if (!incoming && !outgoing)
        {
            throw new InvalidOperationException($"Packet {type.FullName} has no direction contract.");
        }

        var direction = incoming && outgoing ? PacketDirection.Both : incoming
            ? PacketDirection.Incoming : PacketDirection.Outgoing;
        var minimum = attribute.Sizing == PacketSizing.Fixed ? attribute.Length : attribute.MinimumLength;
        return Row(type, attribute.OpCode, attribute.Sizing,
            attribute.Sizing == PacketSizing.Fixed ? attribute.Length : null,
            minimum, direction, attribute.Description);
    }

    private static object Row(
        Type type, byte opcode, PacketSizing sizing, int? fixedLength, int minimumLength,
        PacketDirection direction, string? description)
    {
        var assembly = type.Assembly.GetName().Name!;
        var namespaceTail = type.Namespace![assembly.Length..].TrimStart('.').Replace('.', '/');
        var source = $"src/{assembly}/{namespaceTail}/{type.Name}.cs";
        var subcommand = type.GetField("Subcommand", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        var id = $"0x{opcode:X2}";
        if (subcommand?.IsLiteral == true)
        {
            id += $"/0x{Convert.ToInt32(subcommand.GetRawConstantValue()):X2}";
        }

        return new
        {
            id,
            name = type.Name,
            direction = direction.ToString().ToLowerInvariant(),
            sizing = sizing.ToString().ToLowerInvariant(),
            fixedLength,
            minimumLength,
            description = description ?? "",
            source
        };
    }
}
