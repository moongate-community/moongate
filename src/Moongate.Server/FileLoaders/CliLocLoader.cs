using System.Text;
using Moongate.Server.Attributes;
using Moongate.UO.Data.Context;
using Moongate.UO.Data.Files;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.UO.Data.Localization;
using Moongate.UO.Data.Utils;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Represents CliLocLoader.
/// </summary>
[RegisterFileLoader(10)]
public class CliLocLoader : IFileLoader
{
    private const uint MythicHeaderXor = 0x8E2C9A3D;
    private const int MaxExpectedMythicOutputLength = 64 * 1024 * 1024;
    private const int MaxEntryLength = 64 * 1024;

    private readonly ILogger _logger = Log.ForContext<CliLocLoader>();

    public Task LoadAsync()
    {
        var cliLocFile = UoFiles.FindDataFile("cliloc.enu");

        var entries = ReadCliLocFile(cliLocFile, true);

        UOContext.LocalizedMessages = entries.ToDictionary(
            entry => entry.Number,
            entry => entry
        );

        _logger.Information("Loaded {Count} localized messages from {FilePath}", entries.Count, cliLocFile);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads and parses a cliloc.enu file returning a list of CliLocEntry objects
    /// </summary>
    /// <param name="filePath">Path to the cliloc.enu file</param>
    /// <returns>List of parsed cliloc entries</returns>
    public static List<StringEntry> ReadCliLocFile(string filePath, bool decompress)
    {
        var entries = new List<StringEntry>();

        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var buffer = new byte[fileStream.Length];
            _ = fileStream.Read(buffer, 0, buffer.Length);

            var clilocData = buffer;

            if (decompress && IsLikelyMythicCompressed(buffer))
            {
                try
                {
                    var decompressed = MythicDecompress.Decompress(buffer);

                    if (decompressed.Length > 0)
                    {
                        clilocData = decompressed;
                    }
                }
                catch
                {
                    // Some distributions ship cliloc as plain data.
                    // Fallback to raw parsing to avoid startup failure on unexpected formats.
                    clilocData = buffer;
                }
            }

            using (var reader = new BinaryReader(new MemoryStream(clilocData)))
            {
                _ = reader.ReadInt32();
                _ = reader.ReadInt16();

                while (reader.BaseStream.Length != reader.BaseStream.Position)
                {
                    var remaining = reader.BaseStream.Length - reader.BaseStream.Position;

                    if (remaining < 7)
                    {
                        break;
                    }

                    var number = reader.ReadInt32();
                    var flag = reader.ReadByte();
                    var length = (int)reader.ReadUInt16();

                    if (length <= 0 || length > MaxEntryLength)
                    {
                        break;
                    }

                    if (reader.BaseStream.Length - reader.BaseStream.Position < length)
                    {
                        break;
                    }

                    var textBuffer = reader.ReadBytes(length);

                    if (textBuffer.Length != length)
                    {
                        break;
                    }

                    var text = Encoding.UTF8.GetString(textBuffer, 0, length);

                    var se = new StringEntry(number, text, flag);
                    entries.Add(se);
                }
            }
        }

        return entries;
    }

    private static bool IsLikelyMythicCompressed(byte[] buffer)
    {
        if (buffer.Length <= 1028)
        {
            return false;
        }

        var header = BitConverter.ToUInt32(buffer, 0);
        var expectedOutputLength = (int)(header ^ MythicHeaderXor);

        return expectedOutputLength > 0 && expectedOutputLength <= MaxExpectedMythicOutputLength;
    }
}
