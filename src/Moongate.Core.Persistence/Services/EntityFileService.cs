using Moongate.Core.Directories;
using Moongate.Core.Persistence.Interfaces.Entities;
using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Persistence.Io;
using Moongate.Core.Server.Types;

namespace Moongate.Core.Persistence.Services;

    /// <summary>
    /// High-level service for managing Moongate binary files
    /// </summary>
    public class MoongateEntityFileService : IEntityFileService
    {
        private readonly string _dataDirectory;
        private readonly IEntityReader _entityReader;
        private readonly IEntityWriter _entityWriter;

        public MoongateEntityFileService(DirectoriesConfig directoriesConfig, IEntityReader entityReader, IEntityWriter entityWriter)
        {
            _entityReader = entityReader;
            _entityWriter = entityWriter;
            _dataDirectory = directoriesConfig[DirectoryType.Saves];
        }

        /// <summary>
        /// Save entities to binary file
        /// </summary>
        public async Task SaveEntitiesAsync<T>(string fileName, IEnumerable<T> entities) where T : class
        {
            ArgumentNullException.ThrowIfNull(entities);

            var filePath = GetFilePath(fileName);

            /// Create backup if file exists
            //await CreateBackupIfExistsAsync(filePath);

            try
            {
                await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                using var writer = new MoongateFileWriter(fileStream, _entityWriter);

                foreach (var entity in entities)
                {
                    writer.AddEntity(entity);
                }

                await writer.WriteAsync();
                /// Ensure all data is flushed to disk
            }
            catch
            {
                /// Restore backup if save failed
                await RestoreBackupAsync(filePath);
                throw;
            }
            finally
            {
                /// Clean up backup
                await CleanupBackupAsync(filePath);
            }
        }

        /// <summary>
        /// Load entities from binary file
        /// </summary>
        public async Task<List<T>> LoadEntitiesAsync<T>(string fileName) where T : class
        {
            var filePath = GetFilePath(fileName);

            if (!File.Exists(filePath))
                return new List<T>();

            try
            {
                await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var reader = new MoongateFileReader(fileStream, _entityReader);

                reader.RegisterType<T>();
                await reader.LoadAsync();

                return await reader.GetEntitiesAsync<T>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load entities from file '{fileName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Check if file exists
        /// </summary>
        public bool FileExists(string fileName)
        {
            var filePath = GetFilePath(fileName);
            return File.Exists(filePath);
        }

        /// <summary>
        /// Delete file if exists
        /// </summary>
        public async Task<bool> DeleteFileAsync(string fileName)
        {
            var filePath = GetFilePath(fileName);

            if (!File.Exists(filePath))
                return false;

            try
            {
                /// Create backup before deletion
                await CreateBackupIfExistsAsync(filePath);

                File.Delete(filePath);
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete file '{fileName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get file info (size, creation date, etc.)
        /// </summary>
        public FileInfo? GetFileInfo(string fileName)
        {
            var filePath = GetFilePath(fileName);

            if (!File.Exists(filePath))
                return null;

            return new FileInfo(filePath);
        }

        /// <summary>
        /// List all entity files in directory
        /// </summary>
        public IEnumerable<string> GetEntityFiles(string pattern = "*.mga")
        {
            return Directory.GetFiles(_dataDirectory, pattern)
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>();
        }

        #region Private Helper Methods

        /// <summary>
        /// Get full file path for given filename
        /// </summary>
        private string GetFilePath(string fileName)
        {
            /// Ensure .mga extension
            if (!fileName.EndsWith(".mga", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".mga";
            }

            return Path.Combine(_dataDirectory, fileName);
        }

        /// <summary>
        /// Create backup of existing file
        /// </summary>
        private async Task CreateBackupIfExistsAsync(string filePath)
        {
            if (File.Exists(filePath))
            {
                var backupPath = filePath + ".backup";

                /// Remove old backup if exists
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                File.Copy(filePath, backupPath);
            }
        }

        /// <summary>
        /// Restore backup file
        /// </summary>
        private async Task RestoreBackupAsync(string filePath)
        {
            var backupPath = filePath + ".backup";

            if (File.Exists(backupPath))
            {
                /// Remove corrupted file
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                /// Restore backup
                File.Move(backupPath, filePath);
            }
        }

        /// <summary>
        /// Clean up backup file
        /// </summary>
        private async Task CleanupBackupAsync(string filePath)
        {
            var backupPath = filePath + ".backup";

            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }

        #endregion
    }
