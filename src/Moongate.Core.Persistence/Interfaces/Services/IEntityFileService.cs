namespace Moongate.Core.Persistence.Interfaces.Services;

    /// <summary>
    /// Interface for entity file service operations
    /// </summary>
    public interface IEntityFileService
    {
        /// <summary>
        /// Save entities to binary file
        /// </summary>
        Task SaveEntitiesAsync<T>(string fileName, IEnumerable<T> entities) where T : class;

        /// <summary>
        /// Load entities from binary file
        /// </summary>
        Task<List<T>> LoadEntitiesAsync<T>(string fileName) where T : class;

        /// <summary>
        /// Check if file exists
        /// </summary>
        bool FileExists(string fileName);

        /// <summary>
        /// Delete file if exists
        /// </summary>
        Task<bool> DeleteFileAsync(string fileName);

        /// <summary>
        /// Get file info (size, creation date, etc.)
        /// </summary>
        FileInfo? GetFileInfo(string fileName);

        /// <summary>
        /// List all entity files in directory
        /// </summary>
        IEnumerable<string> GetEntityFiles(string pattern = "*.mga");
    }
