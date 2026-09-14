using System.Reflection;

namespace Moongate.Core.Utils;

/// <summary>
/// Provides utility methods for reading assembly version metadata.
/// </summary>
public static class VersionUtils
{
    /// <summary>
    /// Gets the informational version of the Nocturnia.Core assembly.
    /// </summary>
    /// <returns>The version declared for Nocturnia.Core, without build metadata.</returns>
    public static string GetVersion()
        => GetVersion(typeof(VersionUtils).Assembly);

    /// <summary>
    /// Gets the informational version of the specified assembly, stripping any build metadata
    /// after the <c>+</c> separator (e.g. the source revision appended by SourceLink).
    /// </summary>
    /// <param name="assembly">The assembly to read version metadata from.</param>
    /// <returns>
    /// The assembly informational version, or the assembly version when informational metadata
    /// is unavailable, or an empty string when neither is present.
    /// </returns>
    public static string GetVersion(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                           ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var metadataIndex = informationalVersion.IndexOf('+', StringComparison.Ordinal);

            return metadataIndex == -1 ? informationalVersion : informationalVersion[..metadataIndex];
        }

        return assembly.GetName().Version?.ToString() ?? "";
    }
}
