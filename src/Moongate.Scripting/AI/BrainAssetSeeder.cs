using System.Reflection;

namespace Moongate.Scripting.AI;

public static class BrainAssetSeeder
{
    private const string ResourcePrefix = "Moongate.Scripting.Assets.Brains.";

    public static void SeedMissing(string brainsDirectory)
    {
        Directory.CreateDirectory(brainsDirectory);

        var assembly = typeof(BrainAssetSeeder).Assembly;
        var resources = assembly
                        .GetManifestResourceNames()
                        .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                        .Where(name => name.EndsWith(".lua", StringComparison.Ordinal))
                        .Order(StringComparer.Ordinal);

        foreach (var resourceName in resources)
        {
            SeedMissing(assembly, resourceName, brainsDirectory);
        }
    }

    private static void SeedMissing(Assembly assembly, string resourceName, string brainsDirectory)
    {
        var fileName = resourceName[ResourcePrefix.Length..];
        var destination = Path.Combine(brainsDirectory, fileName);

        if (File.Exists(destination))
        {
            return;
        }

        var temporaryPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            using (var source = assembly.GetManifestResourceStream(resourceName) ??
                                throw new InvalidOperationException(
                                    $"Embedded brain resource was not found: {resourceName}"
                                ))
            {
                using (var temporary = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    source.CopyTo(temporary);
                }
            }

            try
            {
                File.Move(temporaryPath, destination);
            }
            catch (IOException) when (File.Exists(destination)) { }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
