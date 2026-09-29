using Tomlyn.Model;

namespace Moongate.Server.Core.Data.Config;

/// <summary>
///     The parsed <c>config/moongate.toml</c>, given to plugins so they can read their own sections, and the section
///     names already taken by the host and by other plugins.
/// </summary>
public sealed class ServerConfigDocument
{
    private readonly HashSet<string> _sections;

    /// <summary>
    ///     Gets the path of the configuration file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    ///     Gets the parsed file; a section appended with its defaults is added here too.
    /// </summary>
    public TomlTable Table { get; }

    /// <summary>
    ///     Creates the document with the section names the host already owns.
    /// </summary>
    public ServerConfigDocument(string filePath, TomlTable table, IEnumerable<string> reservedSections)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(reservedSections);

        FilePath = filePath;
        Table = table;
        _sections = new(reservedSections, StringComparer.Ordinal);
    }

    /// <summary>
    ///     Takes a section name; returns false when the host or another plugin already has it.
    /// </summary>
    public bool TryReserve(string section)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        return _sections.Add(section);
    }
}
