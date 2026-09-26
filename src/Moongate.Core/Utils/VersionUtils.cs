using System.Reflection;

namespace Moongate.Core.Utils;

/// <summary>
///     Provides utility methods for reading assembly version and codename metadata.
/// </summary>
public static class VersionUtils
{
    /// <summary>
    ///     Gets the codename embedded in the specified assembly's metadata.
    /// </summary>
    /// <param name="assembly">
    ///     The assembly to read codename metadata from.
    /// </param>
    /// <returns>
    ///     The codename, or an empty string when the metadata is unavailable.
    /// </returns>
    public static string GetCodename(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                   .FirstOrDefault(attribute => attribute.Key == "Codename")
                   ?.Value ??
               "";
    }

    /// <summary>
    ///     Gets the informational version of the Moongate.Core assembly.
    /// </summary>
    /// <returns>
    ///     The version declared for Moongate.Core, without build metadata.
    /// </returns>
    public static string GetVersion()
    {
        return GetVersion(typeof(VersionUtils).Assembly);
    }

    /// <summary>
    ///     Gets the informational version of the specified assembly, stripping any build metadata
    ///     after the
    ///     <c>
    ///         +
    ///     </c>
    ///     separator (e.g. the source revision appended by SourceLink).
    /// </summary>
    /// <param name="assembly">
    ///     The assembly to read version metadata from.
    /// </param>
    /// <returns>
    ///     The assembly informational version, or the assembly version when informational metadata
    ///     is unavailable, or an empty string when neither is present.
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
