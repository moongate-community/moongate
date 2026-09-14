using System.Buffers.Binary;
using System.Text;
using Moongate.Core.Primitives;
using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Internal;

internal static class BinaryPersistenceFormat
{
    public const int HeaderSize = 100;
    public const int RecordHeaderSize = 32;
    public const ushort Version = 1;

    public static uint Checksum(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xEDB88320);
            }
        }
        return ~crc;
    }

    public static void WriteHeader(Stream stream, string name, PersistenceFileKind kind, ulong sequence, ulong count)
    {
        Span<byte> header = stackalloc byte[HeaderSize];
        header.Clear();
        "MGSTORE1"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header[8..], Version);
        header[10] = (byte)kind;
        BinaryPrimitives.WriteUInt32LittleEndian(header[12..], (uint)name.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(header[16..], sequence);
        BinaryPrimitives.WriteUInt64LittleEndian(header[24..], count);
        Encoding.ASCII.GetBytes(name, header[32..96]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[96..], Checksum(header[..96]));
        stream.Write(header);
    }

    public static PersistenceHeader ReadHeader(Stream stream, string path, string name, PersistenceFileKind kind)
    {
        Span<byte> header = stackalloc byte[HeaderSize];
        if (ReadAvailable(stream, header) != HeaderSize)
            throw Invalid(path, "Incomplete file header.");
        if (Checksum(header[..96]) != BinaryPrimitives.ReadUInt32LittleEndian(header[96..]))
            throw Invalid(path, "File header checksum mismatch.");
        var version = BinaryPrimitives.ReadUInt16LittleEndian(header[8..]);
        if (version != Version)
            throw Invalid(path, $"Unsupported format version {version}.");
        if (!header[..8].SequenceEqual("MGSTORE1"u8) || header[10] != (byte)kind || header[11] != 0)
            throw Invalid(path, "Invalid file magic, kind or reserved field.");
        var nameLength = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
        if (nameLength != name.Length || !header.Slice(32, name.Length).SequenceEqual(Encoding.ASCII.GetBytes(name)) ||
            header[(32 + name.Length)..96].ContainsAnyExcept((byte)0))
            throw Invalid(path, "Collection name mismatch.");
        var count = BinaryPrimitives.ReadUInt64LittleEndian(header[24..]);
        if (kind == PersistenceFileKind.Journal && count != 0)
            throw Invalid(path, "Journal count must be zero.");
        return new PersistenceHeader(BinaryPrimitives.ReadUInt64LittleEndian(header[16..]), count);
    }

    public static void WriteRecord(Stream stream, PersistenceOperation operation, ulong sequence, Serial id, byte[] payload)
    {
        Span<byte> header = stackalloc byte[RecordHeaderSize];
        header.Clear();
        "MGR1"u8.CopyTo(header);
        header[4] = (byte)operation;
        BinaryPrimitives.WriteUInt64LittleEndian(header[8..], sequence);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], id.Value);
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..], (uint)payload.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], Checksum(payload));
        BinaryPrimitives.WriteUInt32LittleEndian(header[28..], Checksum(header[..28]));
        stream.Write(header);
        stream.Write(payload);
    }

    // Null means an incomplete final frame. The owner decides whether truncation is permitted.
    public static PersistenceRecord? ReadRecord(Stream stream, string path, int maxPayloadBytes)
    {
        Span<byte> header = stackalloc byte[RecordHeaderSize];
        if (ReadAvailable(stream, header) != RecordHeaderSize) return null;
        if (Checksum(header[..28]) != BinaryPrimitives.ReadUInt32LittleEndian(header[28..]))
            throw Invalid(path, "Record header checksum mismatch.");
        var operation = (PersistenceOperation)header[4];
        var id = new Serial(BinaryPrimitives.ReadUInt32LittleEndian(header[16..]));
        var length = BinaryPrimitives.ReadUInt32LittleEndian(header[20..]);
        if (!header[..4].SequenceEqual("MGR1"u8) || header[5..8].ContainsAnyExcept((byte)0) || !id.IsValid ||
            operation is not (PersistenceOperation.Upsert or PersistenceOperation.Delete) ||
            length > maxPayloadBytes || (operation == PersistenceOperation.Upsert ? length == 0 : length != 0))
            throw Invalid(path, "Invalid record header, operation, identity or payload length.");
        if (stream.Length - stream.Position < length) return null;
        var payload = new byte[(int)length];
        if (ReadAvailable(stream, payload) != payload.Length) return null;
        if (Checksum(payload) != BinaryPrimitives.ReadUInt32LittleEndian(header[24..]))
            throw Invalid(path, "Payload checksum mismatch.");
        return new PersistenceRecord(operation, BinaryPrimitives.ReadUInt64LittleEndian(header[8..]), id, payload);
    }

    private static int ReadAvailable(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private static InvalidDataException Invalid(string path, string reason)
    {
        return new InvalidDataException($"{path}: {reason}");
    }
}
