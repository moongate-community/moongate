using System.Text;
using System.Text.Json;
using Moongate.Core.Persistence.Data;
using Moongate.Core.Persistence.Interfaces.Entities;

namespace Moongate.Core.Persistence.Io;

    /// <summary>
    /// Binary file reader for Moongate format
    /// </summary>
    public class MoongateFileReader : IDisposable
    {
        private readonly IEntityReader _entityReader;
        private readonly Stream _stream;
        private readonly Dictionary<ulong, EntityIndexEntry> _index;
        private readonly Dictionary<string, Type> _typeRegistry;
        private bool _disposed;

        public MoongateFileReader(Stream stream, IEntityReader entityReader)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _entityReader = entityReader;
            _index = new Dictionary<ulong, EntityIndexEntry>();
            _typeRegistry = new Dictionary<string, Type>();
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
            using var reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);

            /// Verify header
            await ValidateHeaderAsync(reader);

            /// Read entity count
            var entityCount = await ReadUInt32Async(reader);

            /// Read index
            _index.Clear();
            for (uint i = 0; i < entityCount; i++)
            {
                var typeHash = await ReadUInt64Async(reader);
                var offset = await ReadUInt64Async(reader);

                _index[typeHash] = new EntityIndexEntry(typeHash, offset);
            }
        }

        /// <summary>
        /// Get all entities of specified type
        /// </summary>
        public async Task<List<T>> GetEntitiesAsync<T>() where T : class
        {
            var typeName = typeof(T).FullName ?? typeof(T).Name;
            var typeHash = ComputeTypeHash(typeName);

            var entities = new List<T>();

            /// Find all entities of this type
            foreach (var kvp in _index)
            {
                if (kvp.Value.TypeHash == typeHash)
                {
                    var entity = await ReadEntityAtOffsetAsync<T>(kvp.Value.Offset);
                    if (entity != null)
                        entities.Add(entity);
                }
            }

            return entities;
        }

        /// <summary>
        /// Get entity by type hash
        /// </summary>
        public async Task<T?> GetEntityAsync<T>(ulong typeHash) where T : class
        {
            if (!_index.TryGetValue(typeHash, out var indexEntry))
                return null;

            return await ReadEntityAtOffsetAsync<T>(indexEntry.Offset);
        }

        /// <summary>
        /// Get all entities as objects (mixed types)
        /// </summary>
        public async Task<List<object>> GetAllEntitiesAsync()
        {
            var entities = new List<object>();

            foreach (var indexEntry in _index.Values)
            {
                var entity = await ReadEntityAtOffsetAsObjectAsync(indexEntry.Offset);
                if (entity != null)
                    entities.Add(entity);
            }

            return entities;
        }

        /// <summary>
        /// Read entity at specific file offset
        /// </summary>
        private async Task<T?> ReadEntityAtOffsetAsync<T>(ulong offset) where T : class
        {
            _stream.Seek((long)offset, SeekOrigin.Begin);
            using var reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);

            /// Read type name length
            var typeNameLength = await ReadUInt16Async(reader);

            /// Read type name
            var typeNameBytes = new byte[typeNameLength];
            await _stream.ReadExactlyAsync(typeNameBytes);
            var typeName = Encoding.UTF8.GetString(typeNameBytes);

            /// Read data length
            var dataLength = await ReadUInt32Async(reader);

            /// Read entity data
            var entityData = new byte[dataLength];
            await _stream.ReadExactlyAsync(entityData);

            /// Deserialize
            return _entityReader.DeserializeEntity<T>(entityData);
        }

        /// <summary>
        /// Read entity at specific offset as object (dynamic type resolution)
        /// </summary>
        private async Task<object?> ReadEntityAtOffsetAsObjectAsync(ulong offset)
        {
            _stream.Seek((long)offset, SeekOrigin.Begin);
            using var reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);

            /// Read type name length
            var typeNameLength = await ReadUInt16Async(reader);

            /// Read type name
            var typeNameBytes = new byte[typeNameLength];
            await _stream.ReadExactlyAsync(typeNameBytes);
            var typeName = Encoding.UTF8.GetString(typeNameBytes);

            /// Read data length
            var dataLength = await ReadUInt32Async(reader);

            /// Read entity data
            var entityData = new byte[dataLength];
            await _stream.ReadExactlyAsync(entityData);

            /// Find registered type
            if (!_typeRegistry.TryGetValue(typeName, out var type))
            {
                Console.WriteLine($"Warning: Type '{typeName}' not registered, skipping entity");
                return null;
            }

            /// Deserialize using reflection
            var json = Encoding.UTF8.GetString(entityData);
            return JsonSerializer.Deserialize(json, type);
        }

        /// <summary>
        /// Validate file header
        /// </summary>
        private async Task ValidateHeaderAsync(BinaryReader reader)
        {
            /// Check magic header
            var headerBytes = new byte[MoongateFileFormat.HEADER_MAGIC.Length];
            await _stream.ReadExactlyAsync(headerBytes);

            if (!headerBytes.AsSpan().SequenceEqual(MoongateFileFormat.HEADER_MAGIC))
            {
                throw new InvalidDataException("Invalid file header - not a Moongate file");
            }

            /// Check version
            var version = await ReadUInt32Async(reader);
            if (version != MoongateFileFormat.CURRENT_VERSION)
            {
                throw new InvalidDataException($"Unsupported file version: {version}");
            }
        }

        /// <summary>
        /// Read UInt16 in little-endian format
        /// </summary>
        private async Task<ushort> ReadUInt16Async(BinaryReader reader)
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
        private async Task<uint> ReadUInt32Async(BinaryReader reader)
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
        private async Task<ulong> ReadUInt64Async(BinaryReader reader)
        {
            var bytes = new byte[8];
            await _stream.ReadExactlyAsync(bytes);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return BitConverter.ToUInt64(bytes);
        }

        /// <summary>
        /// Compute hash for entity type name (same as writer)
        /// </summary>
        private static ulong ComputeTypeHash(string typeName)
        {
            const ulong FNV_OFFSET_BASIS = 14695981039346656037UL;
            const ulong FNV_PRIME = 1099511628211UL;

            ulong hash = FNV_OFFSET_BASIS;
            var bytes = Encoding.UTF8.GetBytes(typeName);

            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= FNV_PRIME;
            }

            return hash;
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
