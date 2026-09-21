namespace Moongate.Persistence.Data.Internal;

internal sealed record SchemaSqlStatement(string Sql, IReadOnlyList<string> Tokens);
