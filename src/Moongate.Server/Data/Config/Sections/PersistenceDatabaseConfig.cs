using Moongate.Core.Extensions.Env;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>Configures a PostgreSQL connection, optionally containing environment variable references.</summary>
public sealed class PersistenceDatabaseConfig
{
    public string ConnectionString { get; set; } = "";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException("Persistence connection strings must not be blank.");
        }
    }

    public PersistenceDatabaseOptions ToOptions(PersistenceDatabaseTarget target)
    {
        Validate();
        var template = ConnectionString;
        return new PersistenceDatabaseOptions(target, () => Resolve(template, target));
    }

    private static string Resolve(string template, PersistenceDatabaseTarget target)
    {
        try
        {
            return template.ExpandEnvironmentVariables(requireDefined: true);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"Persistence target '{target}' connection_string: {exception.Message}");
        }
    }
}
