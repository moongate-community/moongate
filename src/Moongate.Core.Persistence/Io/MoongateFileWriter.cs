using System.Text;
using Moongate.Core.Persistence.Data;
using Moongate.Core.Persistence.Interfaces.Entities;
using Serilog;

namespace Moongate.Core.Persistence.Io;

/// <summary>
/// Binary file writer for Moongate format
/// </summary>
public class MoongateFileWriter : IDisposable
{
    private readonly ILogger _logger = Log.ForContext<MoongateFileWriter>();
    private readonly Stream _stream;
    private readonly IEntityWriter _entityWriter;
    private readonly List<EntityDataBlock> _entities;
    private bool _disposed;

    public MoongateFileWriter(Stream stream, IEntityWriter entityWriter)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _entityWriter = entityWriter ?? throw new ArgumentNullException(nameof(entityWriter));
        _entities = new List<EntityDataBlock>();
    }

    /// <summary>
    /// Add entity to be written to file
    /// </summary>
    public void AddEntity<T>(T entity) where T : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        /// Serialize entity using injected writer
        var jsonBytes = _entityWriter.SerializeEntity(entity);

        var typeName = typeof(T).FullName ?? typeof(T).Name;
        var dataBlock = new EntityDataBlock(typeName, jsonBytes);

        _entities.Add(dataBlock);
    }

    /// <summary>
    /// Write all entities to file
    /// </summary>
    public async Task WriteAsync()
    {
        /// Write header
        await WriteHeaderAsync();

        /// Calculate index position and reserve space for index
        var indexStartPos = _stream.Position;
        var indexSize = _entities.Count * MoongateFileFormat.INDEX_ENTRY_SIZE;

        /// Write placeholder index (will be overwritten later)
        var placeholderIndex = new byte[indexSize];
        await _stream.WriteAsync(placeholderIndex);

        /// Build index and write entity data
        var indexEntries = new List<EntityIndexEntry>();

        foreach (var entity in _entities)
        {
            var currentOffset = (ulong)_stream.Position;

            indexEntries.Add(new EntityIndexEntry(entity.DataHash, currentOffset));

            /// Write entity data block
            await WriteEntityDataAsync(entity);
        }

        /// Go back and write the actual index
        var endPosition = _stream.Position;
        _stream.Seek(indexStartPos, SeekOrigin.Begin);

        foreach (var entry in indexEntries)
        {
            await WriteUInt64Async(entry.DataHash);
            await WriteUInt64Async(entry.Offset);
        }

        /// Return to end of file
        _stream.Seek(endPosition, SeekOrigin.Begin);

        /// Force flush to disk
        await _stream.FlushAsync();

        /// Debug logging
        _logger.Debug($"[DEBUG] Written {_entities.Count} entities, file size: {_stream.Position} bytes");
        foreach (var entity in _entities)
        {
            _logger.Debug($"[DEBUG] Entity: {entity.TypeName}, Data size: {entity.DataLength} bytes");
        }
    }

    /// <summary>
    /// Write file header
    /// </summary>
    private async Task WriteHeaderAsync()
    {
        /// Magic header "MOONGATE"
        await _stream.WriteAsync(MoongateFileFormat.HEADER_MAGIC);

        /// Version
        await WriteUInt32Async(MoongateFileFormat.CURRENT_VERSION);

        /// Entity count
        await WriteUInt32Async((uint)_entities.Count);
    }

    /// <summary>
    /// Write entity data block
    /// </summary>
    private async Task WriteEntityDataAsync(EntityDataBlock entity)
    {
        /// Write type name length + type name
        var typeNameBytes = Encoding.UTF8.GetBytes(entity.TypeName);
        await WriteUInt16Async((ushort)typeNameBytes.Length);
        await _stream.WriteAsync(typeNameBytes);

        /// Write data length + data
        await WriteUInt32Async(entity.DataLength);
        await _stream.WriteAsync(entity.Data);
    }

    /// <summary>
    /// Write UInt16 in little-endian format
    /// </summary>
    private async Task WriteUInt16Async(ushort value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        await _stream.WriteAsync(bytes);
    }

    /// <summary>
    /// Write UInt32 in little-endian format
    /// </summary>
    private async Task WriteUInt32Async(uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        await _stream.WriteAsync(bytes);
    }

    /// <summary>
    /// Write UInt64 in little-endian format
    /// </summary>
    private async Task WriteUInt64Async(ulong value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        await _stream.WriteAsync(bytes);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
