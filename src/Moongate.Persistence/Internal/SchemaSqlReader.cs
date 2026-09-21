using Moongate.Persistence.Data.Internal;

namespace Moongate.Persistence.Internal;

/// <summary>Lexes only the SQL subset accepted for automatic schema changes; unsupported quoting fails closed.</summary>
internal static class SchemaSqlReader
{
    public static bool TryRead(string sql, out List<SchemaSqlStatement> statements)
    {
        statements = [];
        List<string> tokens = [];
        var start = 0;
        for (var i = 0; i < sql.Length;)
        {
            if (char.IsWhiteSpace(sql[i]))
            {
                i++;
                continue;
            }

            if (sql.AsSpan(i).StartsWith("--"))
            {
                while (i < sql.Length && sql[i] is not ('\n' or '\r'))
                {
                    i++;
                }

                continue;
            }

            if (sql.AsSpan(i).StartsWith("/*"))
            {
                var depth = 1;
                i += 2;
                while (i < sql.Length && depth > 0)
                {
                    if (sql.AsSpan(i).StartsWith("/*"))
                    {
                        depth++;
                        i += 2;
                    }
                    else if (sql.AsSpan(i).StartsWith("*/"))
                    {
                        depth--;
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }
                }

                if (depth != 0)
                {
                    return false;
                }

                continue;
            }

            if (sql[i] == ';')
            {
                if (tokens.Count > 0)
                {
                    statements.Add(new(sql[start..(i + 1)], tokens.ToArray()));
                }

                tokens.Clear();
                start = ++i;
                continue;
            }

            if (sql[i] is '\'' or '"')
            {
                var begin = i;
                var quote = sql[i++];
                var closed = false;
                while (i < sql.Length)
                {
                    // Backslash escape interpretation depends on session options. Never guess.
                    if (sql[i] == '\\' && quote == '\'')
                    {
                        return false;
                    }

                    if (sql[i++] != quote)
                    {
                        continue;
                    }

                    if (i < sql.Length && sql[i] == quote)
                    {
                        i++;
                        continue;
                    }

                    closed = true;
                    break;
                }

                if (!closed)
                {
                    return false;
                }

                tokens.Add(sql[begin..i]);
                continue;
            }

            if (sql[i] == '$')
            {
                return false;
            }

            if (char.IsAsciiLetterOrDigit(sql[i]) || sql[i] == '_')
            {
                var begin = i++;
                while (i < sql.Length && (char.IsAsciiLetterOrDigit(sql[i]) || sql[i] == '_'))
                {
                    i++;
                }

                tokens.Add(sql[begin..i].ToUpperInvariant());
                continue;
            }

            tokens.Add(sql[i++].ToString());
        }

        if (tokens.Count > 0)
        {
            statements.Add(new(sql[start..], tokens.ToArray()));
        }

        return true;
    }
}
