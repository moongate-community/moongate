using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Extensions;
using Moongate.UO.Data.Packets.GeneralInformation.SubCommands.Base;
using Moongate.UO.Data.Packets.GeneralInformation.SubCommands.Base.Interfaces;
using Moongate.UO.Data.Packets.GeneralInformation.Types;

namespace Moongate.UO.Data.Packets.GeneralInformation;

public class GeneralInformationPacket : BaseUoPacket
{
    public int Length { get; private set; } = -1;

    /// <summary>
    /// Gets the subcommand type
    /// </summary>
    public SubcommandType SubcommandType { get; private set; }

    /// <summary>
    /// Gets the raw subcommand data
    /// </summary>
    public ReadOnlyMemory<byte> SubcommandData { get; private set; }

    /// <summary>
    /// Initializes a new GeneralInformationPacket
    /// </summary>
    public GeneralInformationPacket() : base(0xBF) { }

    /// <summary>
    /// Initializes a new GeneralInformationPacket with subcommand data
    /// </summary>
    /// <param name="subcommandType">Subcommand type</param>
    /// <param name="data">Subcommand data</param>
    public GeneralInformationPacket(SubcommandType subcommandType, ReadOnlyMemory<byte> data) : this()
    {
        SubcommandType = subcommandType;
        SubcommandData = data;
        Length = 5 + data.Length; // 1 + 2 + 2 + data length
    }

    public GeneralInformationPacket(SubcommandType subcommandType, ISubcommandData data) : this()
    {
        SubcommandType = subcommandType;
        SubcommandData = ReadOnlyMemory<byte>.Empty;

        using var writer = new SpanWriter(1, true);
        SubcommandData = data.Write(writer);
        Length = 5 + data.Length;
    }

    /// <summary>
    /// Creates a typed subcommand parser for this packet
    /// </summary>
    /// <returns>Subcommand parser instance</returns>
    public ISubcommandParser CreateParser()
        => new SubcommandParser(SubcommandType, SubcommandData);

    /// <inheritdoc />
    protected override bool Read(SpanReader reader)
    {
        try
        {
            Length = reader.ReadInt16();

            // Read subcommand type
            SubcommandType = (SubcommandType)reader.ReadUInt16();

            // Read remaining data
            var dataLength = Length - 5; // Total length - header (1 + 2 + 2)

            if (dataLength > 0)
            {
                var data = new byte[dataLength];

                for (var i = 0; i < dataLength; i++)
                {
                    data[i] = reader.ReadByte();
                }

                SubcommandData = data;

                this.ParseSubcommandTyped();

                return true;
            }

            SubcommandData = ReadOnlyMemory<byte>.Empty;

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        var startPosition = writer.Position;

        writer.Write(OpCode);
        writer.Write((ushort)Length);
        writer.Write((ushort)SubcommandType);

        if (!SubcommandData.IsEmpty)
        {
            writer.Write(SubcommandData.Span);
        }

        var endPosition = writer.Position;

        return writer.RawBuffer.Slice(startPosition, endPosition - startPosition).ToArray();
    }
}
