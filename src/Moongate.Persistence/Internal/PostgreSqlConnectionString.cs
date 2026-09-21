using Npgsql;

namespace Moongate.Persistence.Internal;

internal static class PostgreSqlConnectionString
{
    public static NpgsqlConnectionStringBuilder Parse(string value)
        => new(Migrations.Services.PostgreSqlConnectionString.Normalize(value));
}
