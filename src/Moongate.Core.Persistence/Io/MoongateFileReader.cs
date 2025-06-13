using System.Text;
using System.Text.Json;
using Moongate.Core.Persistence.Data;
using Moongate.Core.Persistence.Interfaces.Entities;
using Serilog;

namespace Moongate.Core.Persistence.Io;

/// <summary>
/// Binary file reader for Moongate format
/// </summary>
public class MoongateFileReader : IDisposable
{
    private readonly Stream _stream;
    private readonly IEntityReader _entityReader;
    private readonly Dictionary<ulong, EntityIndexEntry> _index;
    private readonly Dictionary<string, Type> _typeRegistry;
    private readonly List<EntityIndexEntry> _allEntries;
    private readonly ILogger _logger = Log.ForContext<MoongateFileReader>();
    private bool _disposed;

    public MoongateFileReader(Stream stream, IEntityReader entityReader)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _entityReader = entityReader ?? throw new ArgumentNullException(nameof(entityReader));
        _index = new Dictionary<ulong, EntityIndexEntry>();
        _typeRegistry = new Dictionary<string, Type>();
        _allEntries = new List<EntityIndexEntry>();
    }

    /// <summary>
    /// Register type for deserialization
    /// </summary>
    public void RegisterType<T>() where T : class
    {
        var typeName = typeof(T).FullName ?? typeof(T).Name;
        _typeRegistry[typeName] = typeof(T);
    }

    /// <summary>
    /// Load and parse file header and index
    /// </summary>
    public async Task LoadAsync()
    {
        _stream.Seek(0, SeekOrigin.Begin);

        /// Verify header
        await ValidateHeaderAsync();

        /// Read entity count
        var entityCount = await ReadUInt32Async();

        /// Read index
        _index.Clear();
        _allEntries.Clear();

        for (uint i = 0; i < entityCount; i++)
        {
            var dataHash = await ReadUInt64Async();
            var offset = await ReadUInt64Async();

            var entry = new EntityIndexEntry(dataHash, offset);
            _index[dataHash] = entry;
            _allEntries.Add(entry);

            _logger.Debug("Loaded index entry: Hash {DataHash:X16} → Offset {Offset}", dataHash, offset);
        }
    }

    /// <summary>
    /// Get all entities of specified type
    /// </summary>
    public async Task<List<T>> GetEntitiesAsync<T>() where T : class
    {
        var typeName = typeof(T).FullName;
        _logger.Debug("Looking for entities of type '{TypeName}'", typeName);
        _logger.Debug("Total entries in index: {AllEntriesCount}", _allEntries.Count);

        var entities = new List<T>();

        /// Check all entities and filter by type after reading
        foreach (var entry in _allEntries)
        {

            var entityInfo = await ReadEntityInfoAtOffsetAsync(entry.Offset);

            if (entityInfo?.TypeName == typeName)
            {
                var entity = _entityReader.DeserializeEntity<T>(entityInfo.Data, typeof(T));
                if (entity != null)
                {
                    entities.Add(entity);
                }
            }
            else
            {
                _logger.Debug("Type mismatch: expected '{TypeName}', got '{EntityInfoTypeName}'", typeName, entityInfo?.TypeName);
            }
        }

        return entities;
    }

    /// <summary>
    /// Get entity by data hash
    /// </summary>
    public async Task<T?> GetEntityAsync<T>(ulong dataHash) where T : class
    {
        if (!_index.TryGetValue(dataHash, out var indexEntry))
            return null;

        var entityInfo = await ReadEntityInfoAtOffsetAsync(indexEntry.Offset);
        return entityInfo != null ? _entityReader.DeserializeEntity<T>(entityInfo.Data, typeof(T)) : null;
    }

    /// <summary>
    /// Get all entities as objects (mixed types)
    /// </summary>
    public async Task<List<object>> GetAllEntitiesAsync()
    {
        var entities = new List<object>();

        foreach (var indexEntry in _allEntries)
        {
            var entity = await ReadEntityAtOffsetAsObjectAsync(indexEntry.Offset);
            if (entity != null)
                entities.Add(entity);
        }

        return entities;
    }

    /// <summary>
    /// Entity info structure for reading
    /// </summary>
    private class EntityInfo
    {
        public string TypeName { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// Read entity info at specific file offset without deserializing
    /// </summary>
    private async Task<EntityInfo?> ReadEntityInfoAtOffsetAsync(ulong offset)
    {
        _stream.Seek((long)offset, SeekOrigin.Begin);

        /// Read type name length
        var typeNameLength = await ReadUInt16Async();

        /// Read type name
        var typeNameBytes = new byte[typeNameLength];
        await _stream.ReadExactlyAsync(typeNameBytes);
        var typeName = Encoding.UTF8.GetString(typeNameBytes);

        /// Read data length
        var dataLength = await ReadUInt32Async();

        /// Read entity data
        var entityData = new byte[dataLength];
        await _stream.ReadExactlyAsync(entityData);

        return new EntityInfo
        {
            TypeName = typeName,
            Data = entityData
        };
    }

    /// <summary>
    /// Read entity at specific offset as object (dynamic type resolution)
    /// </summary>
    private async Task<object?> ReadEntityAtOffsetAsObjectAsync(ulong offset)
    {
        _stream.Seek((long)offset, SeekOrigin.Begin);

        /// Read type name length
        var typeNameLength = await ReadUInt16Async();

        /// Read type name
        var typeNameBytes = new byte[typeNameLength];
        await _stream.ReadExactlyAsync(typeNameBytes);
        var typeName = Encoding.UTF8.GetString(typeNameBytes);

        /// Read data length
        var dataLength = await ReadUInt32Async();

        /// Read entity data
        var entityData = new byte[dataLength];
        await _stream.ReadExactlyAsync(entityData);

        /// Find registered type
        if (!_typeRegistry.TryGetValue(typeName, out var type))
        {
            _logger.Warning("Warning: Type '{TypeName}' not registered, skipping entity", typeName);
            return null;
        }

        /// Deserialize using injected reader with reflection
        return _entityReader.DeserializeEntity<object?>(entityData, type);
    }

    /// <summary>
    /// Validate file header
    /// </summary>
    private async Task ValidateHeaderAsync()
    {
        /// Check magic header
        var headerBytes = new byte[MoongateFileFormat.HEADER_MAGIC.Length];
        await _stream.ReadExactlyAsync(headerBytes);

        if (!headerBytes.AsSpan().SequenceEqual(MoongateFileFormat.HEADER_MAGIC))
        {
            throw new InvalidDataException("Invalid file header - not a Moongate file");
        }

        /// Check version
        var version = await ReadUInt32Async();
        if (version != MoongateFileFormat.CURRENT_VERSION)
        {
            throw new InvalidDataException($"Unsupported file version: {version}");
        }
    }

    /// <summary>
    /// Read UInt16 in little-endian format
    /// </summary>
    private async Task<ushort> ReadUInt16Async()
    {
        var bytes = new byte[2];
        await _stream.ReadExactlyAsync(bytes);

        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);

        return BitConverter.ToUInt16(bytes);
    }

    /// <summary>
    /// Read UInt32 in little-endian format
    /// </summary>
    private async Task<uint> ReadUInt32Async()
    {
        var bytes = new byte[4];
        await _stream.ReadExactlyAsync(bytes);

        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);

        return BitConverter.ToUInt32(bytes);
    }

    /// <summary>
    /// Read UInt64 in little-endian format
    /// </summary>
    private async Task<ulong> ReadUInt64Async()
    {
        var bytes = new byte[8];
        await _stream.ReadExactlyAsync(bytes);

        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);

        return BitConverter.ToUInt64(bytes);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _stream?.Dispose();
            _disposed = true;
        }
    }
}
