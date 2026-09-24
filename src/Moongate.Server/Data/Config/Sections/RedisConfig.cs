using System.Text;
using Moongate.Core.Extensions.Env;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>Configures the shared Redis connection and login handoff proof secret.</summary>
public sealed class RedisConfig
{
    public string ConnectionString { get; set; } = "localhost:6379";

    public string HandoffSecret { get; set; } = "$MOONGATE_HANDOFF_SECRET";

    /// <summary>Resolves the Redis endpoint for process startup without exposing it in errors.</summary>
    public string ResolveConnectionString()
    {
        var value = Resolve(ConnectionString, "redis.connection_string");

        return value;
    }

    /// <summary>Resolves the handoff secret when the process starts.</summary>
    public string ResolveHandoffSecret()
    {
        var value = Resolve(HandoffSecret, "redis.handoff_secret");

        if (Encoding.UTF8.GetByteCount(value) < 32)
        {
            throw new InvalidOperationException("redis.handoff_secret must contain at least 32 bytes.");
        }

        return value;
    }

    /// <summary>Checks configuration templates without requiring secrets during config generation.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException("redis.connection_string is required.");
        }

        if (string.IsNullOrWhiteSpace(HandoffSecret))
        {
            throw new InvalidOperationException("redis.handoff_secret is required.");
        }
    }

    private static string Resolve(string? template, string setting)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException($"{setting} is required.");
        }

        try
        {
            var value = template.ExpandEnvironmentVariables(true);

            return string.IsNullOrWhiteSpace(value)
                       ? throw new InvalidOperationException($"{setting} is required.")
                       : value;
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException($"{setting} cannot be resolved from the environment.");
        }
    }
}
