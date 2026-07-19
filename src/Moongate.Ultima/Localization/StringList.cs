using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Moongate.Ultima.Helpers;
using Moongate.Ultima.Io;

namespace Moongate.Ultima.Localization;

public sealed class StringList
{
    private bool _decompress;
    private int _header1;
    private short _header2;

    public List<StringEntry> Entries { get; private set; }
    public string Language { get; }

    /// <summary>
    /// Non-null when the file was loaded but parsing did not consume the full file cleanly
    /// (e.g. a malformed entry). Contains a human-readable description of where parsing failed
    /// and how many entries were salvaged. Caller should surface this to the user.
    /// </summary>
    public string LoadWarning { get; private set; }

    private Dictionary<int, string> _stringTable;
    private Dictionary<int, StringEntry> _entryTable;

    /// <summary>
    /// Initialize <see cref="StringList" /> of Language
    /// </summary>
    /// <param name="language"></param>
    /// <param name="decompress"></param>
    public StringList(string language, bool decompress)
    {
        _decompress = decompress;
        Language = language;
        LoadEntry(Files.GetFilePath($"cliloc.{language}"));
    }

    /// <summary>
    /// Initialize <see cref="StringList" /> of Language from path
    /// </summary>
    /// <param name="language"></param>
    /// <param name="path"></param>
    /// <param name="decompress"></param>
    public StringList(string language, string path, bool decompress)
    {
        _decompress = decompress;
        Language = language;
        LoadEntry(path);
    }

    private struct ParseResult
    {
        public bool Success;
        public int EntriesParsed;
        public List<StringEntry> Entries;
        public Dictionary<int, string> StringTable;
        public Dictionary<int, StringEntry> EntryTable;
        public int Header1;
        public short Header2;
        public string ErrorMessage;
    }

    public class NumberComparer : IComparer<StringEntry>
    {
        private readonly bool _sortDescending;

        public NumberComparer(bool sortDescending)
        {
            _sortDescending = sortDescending;
        }

        public int Compare(StringEntry x, StringEntry y)
        {
            if (x.Number == y.Number)
            {
                return 0;
            }

            if (_sortDescending)
            {
                return x.Number < y.Number ? 1 : -1;
            }

            return x.Number < y.Number ? -1 : 1;
        }
    }

    public class FlagComparer : IComparer<StringEntry>
    {
        private readonly bool _sortDescending;

        public FlagComparer(bool sortDescending)
        {
            _sortDescending = sortDescending;
        }

        public int Compare(StringEntry x, StringEntry y)
        {
            if ((byte)x.Flag == (byte)y.Flag)
            {
                if (x.Number == y.Number)
                {
                    return 0;
                }

                if (_sortDescending)
                {
                    return x.Number < y.Number ? 1 : -1;
                }

                return x.Number < y.Number ? -1 : 1;
            }

            if (_sortDescending)
            {
                return (byte)x.Flag < (byte)y.Flag ? 1 : -1;
            }

            return (byte)x.Flag < (byte)y.Flag ? -1 : 1;
        }
    }

    public class TextComparer : IComparer<StringEntry>
    {
        private readonly bool _sortDescending;

        public TextComparer(bool sortDescending)
        {
            _sortDescending = sortDescending;
        }

        public int Compare(StringEntry x, StringEntry y)
            => _sortDescending
                   ? string.CompareOrdinal(y.Text, x.Text)
                   : string.CompareOrdinal(x.Text, y.Text);
    }

    public StringEntry GetEntry(int number)
        => _entryTable?.ContainsKey(number) != true ? null : _entryTable[number];

    public string GetString(int number)
        => _stringTable?.ContainsKey(number) != true ? null : _stringTable[number];

    /// <summary>
    /// Saves <see cref="SaveStringList" /> to fileName
    /// </summary>
    /// <param name="fileName"></param>
    public void SaveStringList(string fileName)
    {
        using (var memoryStream = new MemoryStream())
        {
            using (var bin = new BinaryWriter(memoryStream))
            {
                // Sort entries by number
                Entries.Sort(new NumberComparer(false));

                // Write each entry to the memory stream
                foreach (var entry in Entries)
                {
                    bin.Write(entry.Number);
                    bin.Write((byte)entry.Flag);
                    var utf8String = Encoding.UTF8.GetBytes(entry.Text);
                    var length = (ushort)utf8String.Length;
                    bin.Write(length);
                    bin.Write(utf8String);
                }
            }

            var data = memoryStream.ToArray();

            if (_decompress)
            {
                var data2 = new byte[data.Length + 6];

                using (var ms = new MemoryStream(data2))
                {
                    using (var bin = new BinaryWriter(ms))
                    {
                        bin.Write(_header1);
                        bin.Write(_header2);

                        bin.Write(data);
                    }
                }

                var length = (uint)data2.Length;
                data2 = MythicDecompress.Transform(data2);
                var data3 = new byte[data2.Length + 4];

                using (var ms = new MemoryStream(data3))
                {
                    using (var bin = new BinaryWriter(ms))
                    {
                        bin.Write(length ^ 0x8E2C9A3D); // xored decrypted data length
                        bin.Write(data2);
                    }
                }

                using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (var bin = new BinaryWriter(fileStream))
                    {
                        bin.Write(data3);
                    }
                }
            }
            else
            {
                // Write the final output to the file
                using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (var bin = new BinaryWriter(fileStream))
                    {
                        // Write the headers at the beginning
                        bin.Write(_header1);
                        bin.Write(_header2);

                        bin.Write(data);
                    }
                }
            }
        }
    }

    private void Apply(ParseResult result)
    {
        Entries = result.Entries;
        _stringTable = result.StringTable;
        _entryTable = result.EntryTable;
        _header1 = result.Header1;
        _header2 = result.Header2;
    }

    private static string FormatLabel(bool decompress)
        => decompress ? "compressed" : "uncompressed";

    private void LoadEntry(string path)
    {
        if (path == null)
        {
            Entries = new(0);

            return;
        }

        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[fileStream.Length];
        _ = fileStream.Read(buffer, 0, buffer.Length);

        var primary = TryParse(buffer, _decompress);

        if (primary.Success)
        {
            Apply(primary);

            return;
        }

        var fallback = TryParse(buffer, !_decompress);

        if (fallback.Success)
        {
            _decompress = !_decompress;
            Apply(fallback);

            return;
        }

        // Both attempts failed. Prefer whichever extracted more entries — that's the format
        // the file was actually in, just with a corrupt section somewhere.
        var keepPrimary = primary.EntriesParsed >= fallback.EntriesParsed;
        var best = keepPrimary ? primary : fallback;

        if (!keepPrimary)
        {
            _decompress = !_decompress;
        }

        if (best.EntriesParsed > 0)
        {
            Apply(best);
            LoadWarning =
                $"Cliloc '{path}' parsed partially as {FormatLabel(_decompress)}: " +
                $"{best.EntriesParsed} entries recovered before parsing failed. {best.ErrorMessage}";

            return;
        }

        throw new InvalidDataException(
            $"Failed to parse cliloc file '{path}' in either compressed or uncompressed format." +
            $"{Environment.NewLine}  As {FormatLabel(_decompress)}: {primary.ErrorMessage}" +
            $"{Environment.NewLine}  As {FormatLabel(!_decompress)}: {fallback.ErrorMessage}"
        );
    }

    private static ParseResult TryParse(byte[] buffer, bool decompress)
    {
        var result = new ParseResult
        {
            Entries = new(),
            StringTable = new(),
            EntryTable = new()
        };

        byte[] rented = null;

        try
        {
            ReadOnlySpan<byte> data;

            if (decompress)
            {
                var expectedLen = MythicDecompress.PeekDecompressedLength(buffer);

                if (expectedLen == 0 || expectedLen > int.MaxValue)
                {
                    result.ErrorMessage = "decompression failed: invalid header.";

                    return result;
                }

                rented = ArrayPool<byte>.Shared.Rent((int)expectedLen);

                if (!MythicDecompress.TryDecompress(buffer, rented.AsSpan(0, (int)expectedLen), out var written))
                {
                    result.ErrorMessage = "decompression failed.";

                    return result;
                }

                data = rented.AsSpan(0, written);
            }
            else
            {
                data = buffer;
            }

            // Header is 4 + 2 bytes.
            if (data.Length < 6)
            {
                result.ErrorMessage = $"file is {data.Length} bytes, smaller than the 6-byte header.";

                return result;
            }

            result.Header1 = BinaryPrimitives.ReadInt32LittleEndian(data);
            result.Header2 = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(4));

            var cursor = 6;
            var lastNumber = -1;

            while (cursor < data.Length)
            {
                var entryStart = cursor;
                var remaining = data.Length - cursor;

                // Each entry header is 4 (number) + 1 (flag) + 2 (length) = 7 bytes.
                if (remaining < 7)
                {
                    result.ErrorMessage =
                        $"unexpected {remaining} trailing byte(s) at offset 0x{entryStart:X} after entry #{lastNumber}; " +
                        $"need 7 bytes for the next entry header.";

                    return result;
                }

                var number = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(cursor));
                var flag = data[cursor + 4];

                // Writer emits ushort; reading as signed Int16 truncates strings ≥32768 bytes to a negative length.
                int length = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(cursor + 5));
                cursor += 7;

                var bodyRemaining = data.Length - cursor;

                if (length > bodyRemaining)
                {
                    result.ErrorMessage =
                        $"entry #{number} at offset 0x{entryStart:X} declares length {length}, " +
                        $"but only {bodyRemaining} byte(s) remain in the file " +
                        $"(previous entry was #{lastNumber}, parsed {result.EntriesParsed} so far).";

                    return result;
                }

                string text;

                try
                {
                    text = Encoding.UTF8.GetString(data.Slice(cursor, length));
                }
                catch (Exception ex)
                {
                    result.ErrorMessage =
                        $"entry #{number} at offset 0x{entryStart:X} has {length} body bytes that are not valid UTF-8: {ex.Message}";

                    return result;
                }

                cursor += length;

                var se = new StringEntry(number, text, flag);
                result.Entries.Add(se);
                result.StringTable[number] = text;
                result.EntryTable[number] = se;
                result.EntriesParsed++;
                lastNumber = number;
            }

            result.Success = true;

            return result;
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
