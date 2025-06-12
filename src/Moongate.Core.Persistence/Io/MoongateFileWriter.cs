using System.Text;
using Moongate.Core.Persistence.Data;
using Moongate.Core.Persistence.Interfaces.Entities;

namespace Moongate.Core.Persistence.Io;



    /// <summary>
    /// Binary file writer for Moongate format
    /// </summary>
    public class MoongateFileWriter : IDisposable
    {
        private readonly IEntityWriter _entityWriter;
        private readonly Stream _stream;
        private readonly List<EntityDataBlock> _entities;
        private bool _disposed;

        public MoongateFileWriter(Stream stream, IEntityWriter entityWriter)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _entityWriter = entityWriter;
            _entities = new List<EntityDataBlock>();
        }

        /// <summary>
        /// Add entity to be written to file
        /// </summary>
        public void AddEntity<T>(T entity) where T : class
        {
            ArgumentNullException.ThrowIfNull(entity);

            /// Serialize entity using JsonUtils
            //var json = JsonUtils.Serialize(entity);
            //var jsonBytes = Encoding.UTF8.GetBytes(json);

            var typeName = typeof(T).FullName ?? typeof(T).Name;
            var dataBlock = new EntityDataBlock(typeName, _entityWriter.SerializeEntity(entity));

            _entities.Add(dataBlock);
        }

        /// <summary>
        /// Write all entities to file
        /// </summary>
        public async Task WriteAsync()
        {
            await using var writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);

            /// Write header
            await WriteHeaderAsync(writer);

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
                var typeHash = ComputeTypeHash(entity.TypeName);
                var currentOffset = (ulong)_stream.Position;

                indexEntries.Add(new EntityIndexEntry(typeHash, currentOffset));

                /// Write entity data block
                await WriteEntityDataAsync(writer, entity);
            }

            /// Go back and write the actual index
            var endPosition = _stream.Position;
            _stream.Seek(indexStartPos, SeekOrigin.Begin);

            foreach (var entry in indexEntries)
            {
                await WriteUInt64Async(writer, entry.TypeHash);
                await WriteUInt64Async(writer, entry.Offset);
            }

            /// Return to end of file
            _stream.Seek(endPosition, SeekOrigin.Begin);
        }

        /// <summary>
        /// Write file header
        /// </summary>
        private async Task WriteHeaderAsync(BinaryWriter writer)
        {
            /// Magic header "MOONGATE"
            await _stream.WriteAsync(MoongateFileFormat.HEADER_MAGIC);

            /// Version
            await WriteUInt32Async(writer, MoongateFileFormat.CURRENT_VERSION);

            /// Entity count
            await WriteUInt32Async(writer, (uint)_entities.Count);
        }

        /// <summary>
        /// Write entity data block
        /// </summary>
        private async Task WriteEntityDataAsync(BinaryWriter writer, EntityDataBlock entity)
        {
            /// Write type name length + type name
            var typeNameBytes = Encoding.UTF8.GetBytes(entity.TypeName);
            await WriteUInt16Async(writer, (ushort)typeNameBytes.Length);
            await _stream.WriteAsync(typeNameBytes);

            /// Write data length + data
            await WriteUInt32Async(writer, entity.DataLength);
            await _stream.WriteAsync(entity.Data);
        }

        /// <summary>
        /// Write UInt16 in little-endian format
        /// </summary>
        private async Task WriteUInt16Async(BinaryWriter writer, ushort value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            await _stream.WriteAsync(bytes);
        }

        /// <summary>
        /// Write UInt32 in little-endian format
        /// </summary>
        private async Task WriteUInt32Async(BinaryWriter writer, uint value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            await _stream.WriteAsync(bytes);
        }

        /// <summary>
        /// Write UInt64 in little-endian format
        /// </summary>
        private async Task WriteUInt64Async(BinaryWriter writer, ulong value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            await _stream.WriteAsync(bytes);
        }

        /// <summary>
        /// Compute hash for entity type name
        /// </summary>
        private static ulong ComputeTypeHash(string typeName)
        {
            /// Simple FNV-1a hash for type names
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
                /// BinaryWriter will dispose the stream if we don't use leaveOpen
                _disposed = true;
            }
        }
    }
