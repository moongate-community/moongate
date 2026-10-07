using System.Text;

namespace Moongate.Persistence.Internal;

/// <summary>
///     The table and column comments of a schema comparison. FreeSql takes them from the XML docs, which a single-file
///     build cannot read, so the comparison leaves them out: the reviewed SQL writes the comments the database keeps.
/// </summary>
internal static class SchemaComments
{
    /// <summary>
    ///     Gets whether <paramref name="tokens" /> are a <c>COMMENT ON COLUMN</c> or <c>COMMENT ON TABLE</c> statement.
    /// </summary>
    public static bool IsComment(IReadOnlyList<string> tokens)
    {
        return tokens.Count >= 7 && tokens[0] == "COMMENT" && tokens[1] == "ON" && tokens[2] is "COLUMN" or "TABLE" &&
               tokens[^2] == "IS";
    }

    /// <summary>
    ///     Gets whether <paramref name="tokens" /> remove a comment ( <c>IS NULL</c> or <c>IS ''</c>): what a model
    ///     without
    ///     its XML docs asks for, never a change to keep.
    /// </summary>
    public static bool IsRemoval(IReadOnlyList<string> tokens)
    {
        return IsComment(tokens) && tokens[^1] is "NULL" or "''";
    }

    /// <summary>
    ///     Gets <paramref name="ddl" /> without its comment statements; unchanged when it cannot be read.
    /// </summary>
    public static string Strip(string ddl)
    {
        if (string.IsNullOrWhiteSpace(ddl) || !SchemaSqlReader.TryRead(ddl, out var statements))
        {
            return ddl;
        }

        var kept = new StringBuilder();

        foreach (var statement in statements.Where(statement => !IsComment(statement.Tokens)))
        {
            kept.AppendLine(statement.Sql);
        }

        return kept.ToString();
    }
}
