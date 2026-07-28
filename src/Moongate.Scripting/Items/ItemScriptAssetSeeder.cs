using System.Reflection;

namespace Moongate.Scripting.Items;

/// <summary>
/// Copies the shipped item scripts into the runtime directory, skipping any that already exist so a
/// server owner's edits are never overwritten. The sibling of <c>BrainAssetSeeder</c>.
/// </summary>
public static class ItemScriptAssetSeeder
{
    private const string ResourcePrefix = "Moongate.Scripting.Assets.Items.";

    public static void SeedMissing(string itemsDirectory)
    {
        Directory.CreateDirectory(itemsDirectory);

        var assembly = typeof(ItemScriptAssetSeeder).Assembly;
        var resources = assembly
                        .GetManifestResourceNames()
                        .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                        .Where(name => name.EndsWith(".lua", StringComparison.Ordinal))
                        .Order(StringComparer.Ordinal);

        foreach (var resourceName in resources)
        {
            SeedMissing(assembly, resourceName, itemsDirectory);
        }
    }

    private static void SeedMissing(Assembly assembly, string resourceName, string itemsDirectory)
    {
        var fileName = resourceName[ResourcePrefix.Length..];
        var destination = Path.Combine(itemsDirectory, fileName);

        if (File.Exists(destination))
        {
            return;
        }

        // Write beside the target and move into place, so a reader never sees a half-written script.
        var temporaryPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            using (var source = assembly.GetManifestResourceStream(resourceName) ??
                                throw new InvalidOperationException(
                                    $"Embedded item script resource was not found: {resourceName}"
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
