namespace Moongate.Persistence.Data.Internal;

/// <summary>
///     One table of a data export: where it is and which columns are written.
/// </summary>
internal sealed class PostgreSqlExportTable
{
    public long Oid { get; init; }

    public string Schema { get; init; } = "";

    public string Name { get; init; } = "";

    public List<string> Columns { get; } = [];

    /// <summary>
    ///     Gets the schema-qualified name, always quoted.
    /// </summary>
    public string QuotedName => $"{Quote(Schema)}.{Quote(Name)}";

    public static string Quote(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
