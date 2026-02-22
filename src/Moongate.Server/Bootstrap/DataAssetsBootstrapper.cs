using Serilog;

namespace Moongate.Server.Bootstrap;

/// <summary>
/// Copies bundled data assets into the configured data directory when missing.
/// Existing files are never overwritten.
/// </summary>
public static class DataAssetsBootstrapper
{
    /// <summary>
    /// Copies bundled assets from a source root to a destination root when missing.
    /// Existing files are never overwritten.
    /// </summary>
    public static int EnsureAssets(
        string sourceDirectory,
        string destinationDirectory,
        ILogger logger,
        string assetLabel = "Assets"
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(sourceDirectory))
        {
            logger.Warning("{AssetLabel} source directory not found: {SourceDirectory}", assetLabel, sourceDirectory);

            return 0;
        }

        Directory.CreateDirectory(destinationDirectory);

        var copiedFiles = 0;
        var sourceFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);

        foreach (var sourceFile in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
            var destinationFile = Path.Combine(destinationDirectory, relativePath);
            var destinationFileDirectory = Path.GetDirectoryName(destinationFile);

            if (!string.IsNullOrWhiteSpace(destinationFileDirectory))
            {
                Directory.CreateDirectory(destinationFileDirectory);
            }

            if (File.Exists(destinationFile))
            {
                continue;
            }

            File.Copy(sourceFile, destinationFile);
            copiedFiles++;
        }

        logger.Information(
            "{AssetLabel} synchronization completed. Copied {CopiedFiles} missing files into {DestinationDirectory}",
            assetLabel,
            copiedFiles,
            destinationDirectory
        );

        return copiedFiles;
    }

    public static int EnsureDataAssets(string sourceDataDirectory, string destinationDataDirectory, ILogger logger)
        => EnsureAssets(sourceDataDirectory, destinationDataDirectory, logger, "Data assets");
}
