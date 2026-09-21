using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Tests.TestSupport.Persistence.Stress;

namespace Moongate.Persistence.Tests.Integration.Stress;

[Collection(PostgreSqlCollection.Name)]
public sealed class PersistenceStressScenarioTests
{
    private readonly PostgreSqlFixture _postgres;

    public PersistenceStressScenarioTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task Run_SmallConcurrentWorkload_VerifiesCommittedAndRolledBackDataAfterReopen()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        var result = await PersistenceStressScenario.RunAsync(database, 4, 2, 1, 0, operationsPerSession: 20);
        Assert.True(result.Verified);
        Assert.Empty(result.Errors);
        Assert.Equal(4, result.Sessions);
        Assert.Equal(4, result.CommittedTransactions);
        Assert.Equal(4, result.RolledBackTransactions);
        Assert.Equal(80, result.Operations.Values.Sum(operation => operation.Successes));
        Assert.True(result.Operations.Values.All(operation => operation.Successes > 0));
    }
}
