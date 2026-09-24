using FreeSql;
using FreeSql.Internal;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Internal;

internal sealed class PostgreSqlDatabase : IDisposable
{
    private bool _disposed;

    public PersistenceDatabaseTarget Target { get; }

    public string RuntimeConnectionString { get; }

    public string SchemaConnectionString { get; }

    public IFreeSql Orm { get; }

    private PostgreSqlDatabase(
        PersistenceDatabaseTarget target,
        string runtimeConnectionString,
        string schemaConnectionString,
        IFreeSql orm
    )
    {
        Target = target;
        RuntimeConnectionString = runtimeConnectionString;
        SchemaConnectionString = schemaConnectionString;
        Orm = orm;
    }

    public static PostgreSqlDatabase Create(PersistenceDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var runtimeConnectionString = options.ResolveRuntimeConnectionString();
        var schemaConnectionString = options.ResolveSchemaConnectionString(runtimeConnectionString);
        options.ValidateSameDatabaseEndpoint(runtimeConnectionString, schemaConnectionString);
        SerialTypeHandler.EnsureRegistered();
        var orm = new FreeSqlBuilder()
                  .UseConnectionString(DataType.PostgreSQL, runtimeConnectionString)
                  .UseNameConvert(NameConvertType.PascalCaseToUnderscoreWithLower)
                  .UseAutoSyncStructure(false)
                  .UseNoneCommandParameter(false)
                  .Build();

        return new(options.Target, runtimeConnectionString, schemaConnectionString, orm);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Orm.Dispose();
    }
}
