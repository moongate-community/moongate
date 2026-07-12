using System.Reflection;
using SquidStd.Core.Utils;

namespace Moongate.Server.Internal;

internal static class EmbeddedResourceDirectorySeeder
{
    public static void SeedAtomic(
        Assembly assembly,
        string embeddedDirectory,
        string embeddedNamespace,
        string destinationDirectory
    )
    {
        var normalizedDestination = Path.GetFullPath(destinationDirectory);
        var parentDirectory = Path.GetDirectoryName(normalizedDestination)!;
        var directoryName = Path.GetFileName(normalizedDestination);
        var temporaryDirectory = Path.Combine(parentDirectory, $".{directoryName}-{Guid.NewGuid():N}.tmp");
        var temporaryPrefix = temporaryDirectory + Path.DirectorySeparatorChar;
        var resources = ResourceUtils.GetEmbeddedResourceNames(assembly, embeddedDirectory)
            .Where(resourceName => resourceName.StartsWith(embeddedNamespace + ".", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        Directory.CreateDirectory(parentDirectory);

        try
        {
            Directory.CreateDirectory(temporaryDirectory);

            foreach (var resourceName in resources)
            {
                var relativePath = ResourceUtils.ConvertResourceNameToPath(resourceName, embeddedNamespace);
                var destination = Path.GetFullPath(Path.Combine(temporaryDirectory, relativePath));

                if (string.Equals(destination, temporaryDirectory, StringComparison.Ordinal) ||
                    !destination.StartsWith(temporaryPrefix, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Embedded resource '{resourceName}' resolves outside destination root '{normalizedDestination}'."
                    );
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllBytes(
                    destination,
                    ResourceUtils.GetEmbeddedResourceByteArray(assembly, resourceName).ToArray()
                );
            }

            Directory.Move(temporaryDirectory, normalizedDestination);
        }
        catch
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }

            throw;
        }
    }
}
