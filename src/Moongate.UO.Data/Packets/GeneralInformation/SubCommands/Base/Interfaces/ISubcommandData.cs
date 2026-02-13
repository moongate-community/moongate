using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets.GeneralInformation.SubCommands.Base.Interfaces;

/// <summary>
/// Interface for subcommand data structures
/// </summary>
public interface ISubcommandData
{
    /// <summary>
    /// Gets the data length in bytes
    /// </summary>
    int Length { get; }

    /// <summary>
    /// Reads subcommand data from reader
    /// </summary>
    /// <param name="reader">Span reader</param>
    void Read(SpanReader reader);

    /// <summary>
    /// Writes subcommand data to writer
    /// </summary>
    /// <param name="writer">Span writer</param>
    ReadOnlyMemory<byte> Write(SpanWriter writer);
}
