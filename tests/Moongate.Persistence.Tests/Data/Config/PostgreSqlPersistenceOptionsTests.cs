using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Internal;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.Data.Config;

public sealed class PostgreSqlPersistenceOptionsTests
{
    [Fact]
    public void Constructor_DefaultsSchemaSynchronizationOffAndDoesNotResolveConnections()
    {
        var resolutions = 0;
        var database = new PersistenceDatabaseOptions(
            PersistenceDatabaseTarget.Realm,
            () =>
            {
                resolutions++;
                return "Host=localhost;Database=realm;Username=runtime";
            }
        );

        var options = new PostgreSqlPersistenceOptions([database]);

        Assert.False(options.AutoSynchronizeSchema);
        Assert.Equal(0, resolutions);
    }

    [Fact]
    public void ToString_DoesNotExposeConnectionStrings()
    {
        const string connectionMarker = "must-not-appear";
        var database = new PersistenceDatabaseOptions(
            PersistenceDatabaseTarget.Realm,
            $"Host=localhost;Database=realm;Username=runtime;ApplicationName={connectionMarker}",
            $"Host=localhost;Database=realm;Username=schema;ApplicationName={connectionMarker}"
        );
        var options = new PostgreSqlPersistenceOptions([database]);

        Assert.DoesNotContain(connectionMarker, database.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(connectionMarker, options.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CreateDatabase_AuthenticationMayDifferWhenEndpointMatches()
    {
        var options = new PersistenceDatabaseOptions(
            PersistenceDatabaseTarget.Realm,
            "Host=localhost;Port=5432;Database=realm;Username=runtime",
            "Host=LOCALHOST;Port=5432;Database=realm;Username=schema"
        );

        using var database = PostgreSqlDatabase.Create(options);

        Assert.Equal(PersistenceDatabaseTarget.Realm, database.Target);
    }

    [Theory, InlineData(false), InlineData(true)]
    public void CreateDatabase_MalformedConnection_DoesNotRetainSecretInException(bool separateSchema)
    {
        const string marker = "synthetic_marker";
        var malformed = $"postgres://user:{marker}@localhost/database?unknown_option=true";
        var options = new PersistenceDatabaseOptions(
            PersistenceDatabaseTarget.Realm,
            separateSchema ? "Host=localhost;Database=realm;Username=runtime" : malformed,
            separateSchema ? malformed : null
        );
        var error = Assert.Throws<InvalidOperationException>(() => PostgreSqlDatabase.Create(options));
        Assert.DoesNotContain(marker, error.ToString(), StringComparison.Ordinal);
        Assert.Contains(separateSchema ? "schema" : "runtime", error.Message);
        Assert.Contains("Npgsql", error.Message);
    }
}
