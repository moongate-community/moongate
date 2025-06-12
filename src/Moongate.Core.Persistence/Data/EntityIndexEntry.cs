namespace Moongate.Core.Persistence.Data;

     /// <summary>
    /// Index entry for entity location in file
    /// </summary>
    public struct EntityIndexEntry
    {
        /// <summary>
        /// Hash of the entity type name
        /// </summary>
        public ulong TypeHash { get; set; }

        /// <summary>
        /// Offset in file where entity data starts
        /// </summary>
        public ulong Offset { get; set; }

        public EntityIndexEntry(ulong typeHash, ulong offset)
        {
            TypeHash = typeHash;
            Offset = offset;
        }
    }
