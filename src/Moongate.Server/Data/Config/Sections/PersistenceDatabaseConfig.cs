using Moongate.Core.Extensions.Env;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>
///     Configures a PostgreSQL connection, optionally containing environment variable references.
/// </summary>
public sealed class PersistenceDatabaseConfig
{
    public string ConnectionString { get; set; } = "";

    public PersistenceDatabaseOptions ToOptions(PersistenceDatabaseTarget target)
    {
        Validate();
        var template = ConnectionString;

        return new(target, () => Resolve(template, target));
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException("Persistence connection strings must not be blank.");
        }
    }

    private static string Resolve(string template, PersistenceDatabaseTarget target)
    {
        try
        {
            return template.ExpandEnvironmentVariables(true);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"Persistence target '{target}' connection_string: {exception.Message}");
        }
    }
}
