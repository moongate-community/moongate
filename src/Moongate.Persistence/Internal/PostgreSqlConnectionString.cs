using Npgsql;

namespace Moongate.Persistence.Internal;

internal static class PostgreSqlConnectionString
{
    public static NpgsqlConnectionStringBuilder Parse(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return new NpgsqlConnectionStringBuilder(value);
        }

        var uri = new Uri(value, UriKind.Absolute);
        if (!uri.IsWellFormedOriginalString() || uri.Fragment.Length != 0 ||
            uri.AbsolutePath.Length <= 1 || uri.AbsolutePath[1..].Contains('/'))
        {
            throw new FormatException("Invalid PostgreSQL URI.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.DnsSafeHost,
            Port = uri.Port < 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath[1..])
        };
        if (uri.UserInfo.Length > 0)
        {
            var separator = uri.UserInfo.IndexOf(':');
            builder.Username = Uri.UnescapeDataString(separator < 0 ? uri.UserInfo : uri.UserInfo[..separator]);
            if (separator >= 0)
            {
                builder.Password = Uri.UnescapeDataString(uri.UserInfo[(separator + 1)..]);
            }
        }

        foreach (var parameter in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = parameter.IndexOf('=');
            if (separator <= 0)
            {
                throw new FormatException("PostgreSQL URI options require a name and value.");
            }

            var key = Uri.UnescapeDataString(parameter[..separator]).ToLowerInvariant() switch
            {
                "user" => "Username",
                "dbname" => "Database",
                "connect_timeout" => "Timeout",
                "command_timeout" => "Command Timeout",
                "application_name" => "Application Name",
                "search_path" => "Search Path",
                "sslmode" => "SSL Mode",
                "sslrootcert" => "Root Certificate",
                var name => name
            };
            var option = Uri.UnescapeDataString(parameter[(separator + 1)..]);
            if (key.Replace(" ", "").Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                builder.SslMode = Enum.Parse<SslMode>(option.Replace("-", ""), ignoreCase: true);
                if (!Enum.IsDefined(builder.SslMode))
                {
                    throw new FormatException("Unsupported SSL mode.");
                }
            }
            else
            {
                builder[key] = option;
            }
        }

        return builder;
    }
}
