using System.Globalization;
using System.Reflection;

namespace Moongate.Core.Utils;

/// <summary>
///     Provides utility methods for reading assembly version, codename and build metadata.
/// </summary>
public static class VersionUtils
{
    /// <summary>
    ///     The assembly metadata key of the time the assembly was built, in UTC, as ISO 8601.
    /// </summary>
    public const string BuildTimeKey = "BuildTime";

    /// <summary>
    ///     The assembly metadata key of the configuration the assembly was built in: Debug or Release.
    /// </summary>
    public const string BuildConfigurationKey = "BuildConfiguration";

    private const string Unknown = "unknown";

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
        return GetMetadata(assembly, "Codename");
    }

    /// <summary>
    ///     Gets the configuration the specified assembly was built in, as its build wrote it.
    /// </summary>
    /// <param name="assembly">
    ///     The assembly to read build metadata from.
    /// </param>
    /// <returns>
    ///     <c>Debug</c> or <c>Release</c>, or an empty string when the metadata is unavailable.
    /// </returns>
    public static string GetBuildConfiguration(Assembly assembly)
    {
        return GetMetadata(assembly, BuildConfigurationKey);
    }

    /// <summary>
    ///     Gets the time the specified assembly was built at, as its build wrote it.
    /// </summary>
    /// <param name="assembly">
    ///     The assembly to read build metadata from.
    /// </param>
    /// <returns>
    ///     The build time in UTC, or null when the metadata is unavailable or is not a time.
    /// </returns>
    public static DateTimeOffset? GetBuildTime(Assembly assembly)
    {
        return DateTimeOffset.TryParse(
            GetMetadata(assembly, BuildTimeKey),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var built
        )
            ? built
            : null;
    }

    /// <summary>
    ///     Formats a build time as the day and the minute in UTC, such as <c>2026-10-05 14:32 UTC</c>.
    /// </summary>
    /// <param name="time">
    ///     The build time; null when it is not known.
    /// </param>
    /// <returns>
    ///     The formatted time, or <c>unknown</c>.
    /// </returns>
    public static string FormatBuildTime(DateTimeOffset? time)
    {
        return time is { } built
            ? built.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
            : Unknown;
    }

    /// <summary>
    ///     Fills the placeholders of a startup header with the metadata of the specified assembly:
    ///     <c>{Version}</c>, <c>{Codename}</c>, <c>{Configuration}</c> and <c>{BuildTime}</c>.
    /// </summary>
    /// <param name="template">
    ///     The header text with its placeholders.
    /// </param>
    /// <param name="assembly">
    ///     The assembly to read the metadata from.
    /// </param>
    /// <returns>
    ///     The header; a configuration or a build time the assembly does not carry reads <c>unknown</c>.
    /// </returns>
    public static string FormatHeader(string template, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(template);

        var configuration = GetBuildConfiguration(assembly);

        return template.Replace("{Version}", GetVersion(assembly), StringComparison.Ordinal)
                       .Replace("{Codename}", GetCodename(assembly), StringComparison.Ordinal)
                       .Replace("{Configuration}", configuration.Length == 0 ? Unknown : configuration, StringComparison.Ordinal)
                       .Replace("{BuildTime}", FormatBuildTime(GetBuildTime(assembly)), StringComparison.Ordinal);
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

    private static string GetMetadata(Assembly assembly, string key)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                   .FirstOrDefault(attribute => attribute.Key == key)
                   ?.Value ??
               "";
    }
}
