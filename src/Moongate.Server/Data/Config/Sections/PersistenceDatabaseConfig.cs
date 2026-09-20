using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>Names environment variables holding a target's runtime and optional schema connection.</summary>
public sealed class PersistenceDatabaseConfig
{
    public string ConnectionStringEnv { get; set; } = "";
    public string? SchemaConnectionStringEnv { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionStringEnv) ||
            SchemaConnectionStringEnv is not null && string.IsNullOrWhiteSpace(SchemaConnectionStringEnv))
        {
            throw new InvalidOperationException("Persistence connection environment variable names must not be blank.");
        }
    }

    public PersistenceDatabaseOptions ToOptions(PersistenceDatabaseTarget target)
    {
        Validate();
        var runtimeName = ConnectionStringEnv;
        var schemaName = SchemaConnectionStringEnv;
        return new PersistenceDatabaseOptions(target, () => Resolve(runtimeName, target),
            schemaName is null ? null : () => Resolve(schemaName, target));
    }

    private static string Resolve(string name, PersistenceDatabaseTarget target)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Persistence target '{target}' requires environment variable '{name}'.");
        }
        return value;
    }
}
