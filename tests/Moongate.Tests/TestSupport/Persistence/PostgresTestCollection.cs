namespace Moongate.Tests.TestSupport.Persistence;

/// <summary>
///     Every test that touches real Postgres shares this collection, so xUnit runs them one at a time
///     instead of in parallel. CI runs the whole solution's tests against one shared Postgres service;
///     letting every Postgres-backed test class run concurrently (xUnit's default) overwhelms it once
///     enough of them are actually doing real work at once.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresTestCollection
{
    public const string Name = "Postgres";
}
