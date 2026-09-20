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
            });

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
            $"Host=localhost;Database=realm;Username=schema;ApplicationName={connectionMarker}");
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
            "Host=LOCALHOST;Port=5432;Database=realm;Username=schema");

        using var database = PostgreSqlDatabase.Create(options);

        Assert.Equal(PersistenceDatabaseTarget.Realm, database.Target);
    }
}
