using Npgsql;

namespace Moongate.Persistence.Internal;

internal static class PostgreSqlConnectionString
{
    public static NpgsqlConnectionStringBuilder Parse(string value)
    {
        return new(Migrations.Services.PostgreSqlConnectionString.Normalize(value));
    }
}
