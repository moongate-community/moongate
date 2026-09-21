using Moongate.Persistence.Internal;
using Moongate.Persistence.Types.Persistence;
using Npgsql;

namespace Moongate.Persistence.Data.Config;

/// <summary>Provides lazy runtime and schema connections for one persistence database target.</summary>
public sealed class PersistenceDatabaseOptions
{
    private readonly Func<string?> _runtimeConnectionStringFactory;
    private readonly Func<string?> _schemaConnectionStringFactory;
    private readonly bool _hasSeparateSchemaConnection;

    /// <summary>Gets the database target configured by this instance.</summary>
    public PersistenceDatabaseTarget Target { get; }

    /// <summary>
    /// Creates options backed by connection-string values held in memory.
    /// </summary>
    /// <param name="target">The database target.</param>
    /// <param name="runtimeConnectionString">The runtime connection string.</param>
    /// <param name="schemaConnectionString">The optional connection used for schema DDL.</param>
    public PersistenceDatabaseOptions(
        PersistenceDatabaseTarget target,
        string runtimeConnectionString,
        string? schemaConnectionString = null
    ) : this(target, () => runtimeConnectionString, schemaConnectionString is null ? null : () => schemaConnectionString) { }

    /// <summary>
    /// Creates options whose connection strings are resolved only when the target is activated.
    /// </summary>
    /// <param name="target">The database target.</param>
    /// <param name="runtimeConnectionStringFactory">Resolves the runtime connection string.</param>
    /// <param name="schemaConnectionStringFactory">Optionally resolves the connection used for schema DDL.</param>
    public PersistenceDatabaseOptions(
        PersistenceDatabaseTarget target,
        Func<string?> runtimeConnectionStringFactory,
        Func<string?>? schemaConnectionStringFactory = null
    )
    {
        ArgumentNullException.ThrowIfNull(runtimeConnectionStringFactory);

        Target = target;
        _runtimeConnectionStringFactory = runtimeConnectionStringFactory;
        _schemaConnectionStringFactory = schemaConnectionStringFactory ?? runtimeConnectionStringFactory;
        _hasSeparateSchemaConnection = schemaConnectionStringFactory is not null;
    }

    /// <inheritdoc />
    public override string ToString()
        => $"PersistenceDatabaseOptions {{ Target = {Target}, SeparateSchemaConnection = {_hasSeparateSchemaConnection} }}";

    internal string ResolveRuntimeConnectionString()
        => ResolveConnectionString(_runtimeConnectionStringFactory, "runtime");

    internal string ResolveSchemaConnectionString(string runtimeConnectionString)
        => _hasSeparateSchemaConnection
               ? ResolveConnectionString(_schemaConnectionStringFactory, "schema")
               : runtimeConnectionString;

    internal void ValidateSameDatabaseEndpoint(string runtimeConnectionString, string schemaConnectionString)
    {
        var runtime = Parse(runtimeConnectionString, "runtime");
        var schema = Parse(schemaConnectionString, "schema");

        if (!string.Equals(runtime.Host, schema.Host, StringComparison.OrdinalIgnoreCase) ||
            runtime.Port != schema.Port ||
            !string.Equals(runtime.Database, schema.Database, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Persistence target '{Target}' runtime and schema connections must use the same Host, Port and Database endpoint."
            );
        }
    }

    private NpgsqlConnectionStringBuilder Parse(string connectionString, string purpose)
    {
        try
        {
            var parsed = PostgreSqlConnectionString.Parse(connectionString);

            if (string.IsNullOrWhiteSpace(parsed.Host) || string.IsNullOrWhiteSpace(parsed.Database))
            {
                throw new ArgumentException("Host and Database are required.");
            }

            return parsed;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            throw new InvalidOperationException(
                $"Persistence target '{Target}' has an invalid {purpose} PostgreSQL connection string. " +
                "Use postgres://user:password@host:5432/database or Npgsql key=value; format with Host and Database."
            );
        }
    }

    private string ResolveConnectionString(Func<string?> factory, string purpose)
    {
        var connectionString = factory();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Persistence target '{Target}' requires a nonempty {purpose} PostgreSQL connection string."
            );
        }

        return Parse(connectionString, purpose).ConnectionString;
    }
}
