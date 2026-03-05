using Moongate.Stress.Data;

namespace Moongate.Stress.Internal;

public static class StressRunOptionsParser
{
    public static bool TryParse(string[] args, out StressRunOptions options, out string? error)
    {
        options = new StressRunOptions();
        error = null;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Invalid argument '{arg}'.";

                return false;
            }

            var key = arg[2..];

            if (string.Equals(key, "verbose", StringComparison.OrdinalIgnoreCase))
            {
                values[key] = "true";

                continue;
            }

            if (i + 1 >= args.Length)
            {
                error = $"Missing value for '{arg}'.";

                return false;
            }

            values[key] = args[++i];
        }

        if (!TryGetInt(values, "port", 2593, 1, 65535, out var port, out error) ||
            !TryGetInt(values, "clients", 100, 1, 10_000, out var clients, out error) ||
            !TryGetInt(values, "duration", 300, 10, 86_400, out var durationSeconds, out error) ||
            !TryGetInt(values, "ramp-up-per-second", 10, 1, 10_000, out var rampUp, out error) ||
            !TryGetInt(values, "move-interval-ms", 300, 50, 10_000, out var moveIntervalMs, out error))
        {
            options = new StressRunOptions();

            return false;
        }

        var host = GetString(values, "host", "127.0.0.1");
        var userPrefix = GetString(values, "user-prefix", "stress");
        var userPassword = GetString(values, "user-password", "StressPwd#123");
        var userRole = GetString(values, "user-role", "Regular");
        var adminUsername = values.GetValueOrDefault("admin-username");
        var adminPassword = values.GetValueOrDefault("admin-password");

        var httpBaseAddressRaw = GetString(values, "http", "http://localhost:8088");

        if (!Uri.TryCreate(httpBaseAddressRaw, UriKind.Absolute, out var httpBaseAddress))
        {
            error = $"Invalid --http value '{httpBaseAddressRaw}'.";

            options = new StressRunOptions();

            return false;
        }

        options = new StressRunOptions
        {
            Host = host,
            Port = port,
            HttpBaseAddress = httpBaseAddress,
            Clients = clients,
            Duration = TimeSpan.FromSeconds(durationSeconds),
            RampUpPerSecond = rampUp,
            UserPrefix = userPrefix,
            UserPassword = userPassword,
            UserRole = userRole,
            AdminUsername = adminUsername,
            AdminPassword = adminPassword,
            MoveIntervalMs = moveIntervalMs,
            Verbose = values.ContainsKey("verbose")
        };

        return true;
    }

    public static string Usage()
    {
        return """
               Usage:
                 dotnet run --project tools/Moongate.Stress -- [options]

               Options:
                 --host <host>                       Default: 127.0.0.1
                 --port <port>                       Default: 2593
                 --http <url>                        Default: http://localhost:8088
                 --clients <count>                   Default: 100
                 --duration <seconds>                Default: 300
                 --ramp-up-per-second <count>        Default: 10
                 --move-interval-ms <ms>             Default: 300
                 --user-prefix <prefix>              Default: stress
                 --user-password <password>          Default: StressPwd#123
                 --user-role <Regular|Counselor|GameMaster|Administrator>
                 --admin-username <name>             Optional (used when JWT is enabled)
                 --admin-password <password>         Optional (used when JWT is enabled)
                 --verbose                            Verbose client logs
               """;
    }

    private static string GetString(IDictionary<string, string> values, string key, string fallback)
    {
        if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return fallback;
    }

    private static bool TryGetInt(
        IDictionary<string, string> values,
        string key,
        int fallback,
        int min,
        int max,
        out int value,
        out string? error
    )
    {
        error = null;
        value = fallback;

        if (!values.TryGetValue(key, out var raw))
        {
            return true;
        }

        if (!int.TryParse(raw, out var parsed))
        {
            error = $"Invalid --{key} value '{raw}' (not an integer).";

            return false;
        }

        if (parsed < min || parsed > max)
        {
            error = $"Invalid --{key} value '{raw}' (expected range {min}-{max}).";

            return false;
        }

        value = parsed;

        return true;
    }
}
