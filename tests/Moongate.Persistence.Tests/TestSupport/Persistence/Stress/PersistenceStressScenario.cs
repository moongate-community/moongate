using System.Collections.Concurrent;
using System.Diagnostics;
using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Tests.TestSupport.Persistence.Stress.Data;
using Moongate.Persistence.Types.Persistence;
using Npgsql;

namespace Moongate.Persistence.Tests.TestSupport.Persistence.Stress;

internal static class PersistenceStressScenario
{
    public static async Task<StressPhaseReport> RunAsync(
        PostgreSqlTestDatabase database,
        int sessions,
        int concurrency,
        int seconds,
        int thinkMilliseconds,
        int? operationsPerSession = null
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sessions, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrency, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(seconds, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(thinkMilliseconds);
        var states = Enumerable.Range(0, sessions)
                               .Select(
                                   number => new SyntheticSessionEntity
                                   {
                                       SessionNumber = number,
                                       Payload = Payload(number, 0)
                                   }
                               )
                               .ToArray();
        var committedRevisions = Enumerable.Range(0, sessions).Select(_ => new List<long>()).ToArray();
        var measurements = new[] { "read", "query", "update", "commit", "rollback" }
            .ToDictionary(name => name, _ => new LatencyHistogram());
        var admission = new LatencyHistogram();
        var errors = new ConcurrentDictionary<string, long>();
        long commits = 0,
             rollbacks = 0;
        double elapsed,
               cpuSeconds;
        DateTimeOffset startedAtUtc,
                       finishedAtUtc;
        long allocated,
             workingSet;
        using var permits = new SemaphoreSlim(concurrency);
        using var process = Process.GetCurrentProcess();

        using (var container = CreateOwner(database, concurrency, true))
        {
            var owner = container.Resolve<MoongatePersistenceService>();
            var store = container.Resolve<IDataAccess<SyntheticSessionEntity>>();
            await owner.InitializeAsync();

            // Seeding/schema preparation are outside the measured interval; this also warms the connection pool.
            await Parallel.ForEachAsync(
                states,
                new ParallelOptions { MaxDegreeOfParallelism = concurrency },
                async (state, token) => await store.UpsertAsync(state, token)
            );
            Require(states.Select(state => state.Id).Distinct().Count() == sessions, "Duplicate seed identities");
            var cpuBefore = process.TotalProcessorTime;
            var allocatedBefore = GC.GetTotalAllocatedBytes();
            startedAtUtc = DateTimeOffset.UtcNow;
            var started = Stopwatch.GetTimestamp();
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var workers = Enumerable.Range(0, sessions).Select(WorkerAsync).ToArray();
            start.SetResult();
            await Task.WhenAll(workers);
            elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
            finishedAtUtc = DateTimeOffset.UtcNow;
            cpuSeconds = (process.TotalProcessorTime - cpuBefore).TotalSeconds;
            allocated = GC.GetTotalAllocatedBytes() - allocatedBefore;
            process.Refresh();
            workingSet = process.WorkingSet64;
            await owner.DisposeAsync();

            async Task WorkerAsync(int number)
            {
                await start.Task;

                for (var step = 0;
                     operationsPerSession is { } limit
                         ? step < limit
                         : Stopwatch.GetElapsedTime(started).TotalSeconds < seconds;
                     step++)
                {
                    var operation = step % 10;
                    var rollback = operation == 9 && step / 10 % 2 == 1;
                    var name = operation < 5 ? "read" :
                               operation < 7 ? "query" :
                               operation < 9 ? "update" :
                               rollback ? "rollback" : "commit";
                    var queued = Stopwatch.GetTimestamp();
                    var entered = false;
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                    try
                    {
                        await permits.WaitAsync(timeout.Token);
                        entered = true;
                        admission.Record(Stopwatch.GetElapsedTime(queued).TotalMilliseconds);
                        var state = states[number];

                        if (operation < 5)
                        {
                            AssertState(state, await store.GetByIdAsync(state.Id, timeout.Token));
                        }
                        else if (operation < 7)
                        {
                            var found = await store.QueryAsync(row => row.SessionNumber == number, 0, 2, timeout.Token);
                            Require(found.Count == 1, "Session query cardinality");
                            AssertState(state, found[0]);
                        }
                        else
                        {
                            var next = new SyntheticSessionEntity
                            {
                                Id = state.Id,
                                SessionNumber = number,
                                Revision = state.Revision + 1,
                                Payload = Payload(number, state.Revision + 1)
                            };

                            if (operation < 9)
                            {
                                await store.UpsertAsync(next, timeout.Token);
                            }
                            else
                            {
                                try
                                {
                                    await owner.ExecuteInTransactionAsync(
                                        PersistenceDatabaseTarget.Realm,
                                        async transaction =>
                                        {
                                            var entry = new SyntheticWriteEntity
                                            {
                                                SessionNumber = number,
                                                Revision = next.Revision,
                                                RolledBack = rollback
                                            };
                                            await transaction.GetDataAccess<SyntheticWriteEntity>()
                                                             .UpsertAsync(entry, timeout.Token);
                                            Require(entry.Id.IsValid, "Unassigned transaction identity");
                                            await transaction.GetDataAccess<SyntheticSessionEntity>()
                                                             .UpsertAsync(next, timeout.Token);

                                            if (rollback)
                                            {
                                                throw new ExpectedRollbackException();
                                            }
                                        },
                                        timeout.Token
                                    );
                                    Require(!rollback, "Rollback unexpectedly committed");
                                    committedRevisions[number].Add(next.Revision);
                                    Interlocked.Increment(ref commits);
                                }
                                catch (ExpectedRollbackException) when (rollback)
                                {
                                    Interlocked.Increment(ref rollbacks);
                                }
                            }

                            if (!rollback)
                            {
                                states[number] = next;
                            }
                        }

                        // End-to-end logical operation latency includes admission waiting and transaction commit/rollback.
                        measurements[name].Record(Stopwatch.GetElapsedTime(queued).TotalMilliseconds);
                    }
                    catch (Exception exception)
                    {
                        var category = exception is OperationCanceledException or TimeoutException ||
                                       timeout.IsCancellationRequested
                                           ? "timeout"
                                           : exception.GetType().Name;
                        errors.AddOrUpdate(category, 1, (_, count) => count + 1);

                        break; // A commit acknowledgement can be ambiguous: never retry or claim a known state.
                    }
                    finally
                    {
                        if (entered)
                        {
                            permits.Release();
                        }
                    }

                    if (thinkMilliseconds > 0)
                    {
                        await Task.Delay(thinkMilliseconds);
                    }
                }
            }
        }

        var verified = false;

        try
        {
            using var reopened = CreateOwner(database, concurrency, false);
            var owner = reopened.Resolve<MoongatePersistenceService>();
            using var verificationTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await owner.InitializeAsync(verificationTimeout.Token);
            var store = reopened.Resolve<IDataAccess<SyntheticSessionEntity>>();
            var rows = await store.GetAllAsync(verificationTimeout.Token);
            Require(rows.Count == sessions, "Persisted session count");

            foreach (var row in rows)
            {
                Require(row.SessionNumber >= 0 && row.SessionNumber < sessions, "Unknown persisted session");
                AssertState(states[row.SessionNumber], row);
            }

            var writes = reopened.Resolve<IDataAccess<SyntheticWriteEntity>>();
            var entries = await writes.GetAllAsync(verificationTimeout.Token);
            Require(entries.Count == commits, "Persisted transaction count");
            Require(entries.All(entry => entry.Id.IsValid && !entry.RolledBack), "Rollback leaked into persisted rows");
            Require(
                entries.Select(entry => entry.Id).Distinct().Count() == entries.Count,
                "Duplicate transaction identities"
            );
            var bySession = entries.ToLookup(entry => entry.SessionNumber);

            for (var number = 0; number < sessions; number++)
            {
                Require(
                    bySession[number]
                        .Select(entry => entry.Revision)
                        .Order()
                        .SequenceEqual(committedRevisions[number].Order()),
                    "Committed transaction contents differ after reopening"
                );
            }

            await owner.DisposeAsync();
            verified = errors.IsEmpty;
        }
        catch (Exception exception)
        {
            errors.AddOrUpdate($"verification:{exception.GetType().Name}", 1, (_, count) => count + 1);
        }

        var reports = measurements.ToDictionary(pair => pair.Key, pair => Snapshot(pair.Value));

        return new(
            startedAtUtc,
            finishedAtUtc,
            sessions,
            concurrency,
            elapsed,
            reports.Values.Sum(value => value.Successes) / elapsed,
            commits,
            rollbacks,
            allocated,
            cpuSeconds,
            workingSet,
            verified,
            reports,
            Snapshot(admission),
            errors
        );
    }

    private static Container CreateOwner(PostgreSqlTestDatabase database, int concurrency, bool synchronize)
    {
        var connection = new NpgsqlConnectionStringBuilder(database.ConnectionString)
        {
            Pooling = true, MaxPoolSize = concurrency, Timeout = 15, CommandTimeout = 30
        };
        var container = new Container();
        container.RegisterMoongatePersistence(
                     new([new(PersistenceDatabaseTarget.Realm, connection.ConnectionString)], synchronize)
                 )
                 .AddPersistenceWorld<SyntheticSessionEntity>()
                 .AddPersistenceWorld<SyntheticWriteEntity>();

        return container;
    }

    private static StressOperationReport Snapshot(LatencyHistogram histogram)
    {
        return new(histogram.Count, histogram.Percentile(0.50), histogram.Percentile(0.95), histogram.Percentile(0.99));
    }

    private static string Payload(int session, long revision)
    {
        return $"session:{session};revision:{revision};{new('x', 256)}";
    }

    private static void AssertState(SyntheticSessionEntity expected, SyntheticSessionEntity? actual)
    {
        Require(
                actual is not null &&
                actual.Id == expected.Id &&
                actual.SessionNumber == expected.SessionNumber &&
                actual.Revision == expected.Revision &&
                actual.Payload == expected.Payload,
                "Session data mismatch"
            );
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
