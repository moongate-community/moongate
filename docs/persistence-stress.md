# Stress-test PostgreSQL persistence

Run this from a source checkout on Linux or WSL2 with .NET 10, Python 3 and Docker:

```sh
python3 scripts/stress-persistence.py
```

The command builds the existing persistence test project, creates a dedicated
PostgreSQL container, and runs three phases: **100, 500 and 1,000 virtual sessions**,
for 30 seconds each. Allow additional time for image download, schema setup,
seeding, draining pending operations and correctness checks.

This exercises the real `DataAccess<T>`, FreeSql and PostgreSQL implementation.
There is no Ultima Online protocol, TCP connection, `SessionService`, GameLoop,
account authentication, world-save scheduling or server bootstrap in this test.
A virtual session is an asynchronous worker with its own synthetic persisted row.
It is not a connected player or a dedicated PostgreSQL connection.

## Isolation and reproducibility

The script always starts its own pinned PostgreSQL 16 container. It does not accept
an external database address, use the server TOML, or touch the local development
databases. PostgreSQL has no network access or published ports; the test host uses
a temporary Unix socket. Trust authentication is confined to that isolated instance.

The container is limited to **2 CPUs and 1 GiB RAM**, with a disk-backed anonymous
Docker volume. PostgreSQL durability settings remain at their defaults: this is
not an in-memory database and `fsync` is not disabled. Actual storage performance
still depends on the Docker host. The script removes its container, anonymous
volume and temporary socket on completion, Ctrl-C or SIGTERM, while keeping reports.
A machine crash or `kill -9` can bypass cleanup; any leftover container has the
`moongate-stress-` prefix and a unique generated suffix.

Each phase uses a newly created test database and the `stress.sessions` and
`stress.writes` tables. Schema synchronization is enabled only during initial
setup in that disposable database. Reopening for verification disables it.
This scenario measures runtime persistence; it is not a migration validation test.

## Workload

Each session starts with a row containing an automatic `Serial`, a session number,
a revision and a payload of approximately 300 characters. It repeatedly performs:

| Logical operation | Approximate share | Behavior |
| --- | --- | --- |
| Read | 50% | Read its row by ID and validate revision and payload |
| Query | 20% | Filter by indexed session number with bounded paging |
| Update | 20% | Advance its row's revision and payload |
| Commit | 5% | Insert a journal row and update the session in one transaction |
| Rollback | 5% | Attempt both writes, then deliberately abort the transaction |

The mix is deterministic, and short or failed runs can differ from these shares.
Transactions count as one logical operation even though they issue several SQL
commands. Expected rollbacks count as successful operations when the requested
abort completes; unexpected exceptions and timeouts are failures.

Sessions do not concurrently modify the same row. This avoids claiming atomic
increments from an API whose explicit-ID upserts have last-writer-wins semantics.
The benchmark covers concurrent independent sessions and shared database/identity
allocation pressure, not hot-row contention or optimistic concurrency control.

Setup and seeding are outside the measured interval. All session workers are
released together. At most 32 logical operations enter the persistence layer at
once, and each session waits 10 ms after an operation before submitting another.
Each operation has a 30-second cancellation deadline including admission waiting.
At the end of a phase, accepted work drains before persistence is closed.

This is a **closed-loop workload**: slow responses lower the offered request rate.
It does not model a fixed external arrival rate or establish player capacity.
For sustained saturation, use `--think-ms 0` and inspect admission waiting as well
as completed throughput.

## Customize a run

```sh
# A quick smoke run
python3 scripts/stress-persistence.py --sessions 4 --seconds 2 --concurrency 2 --think-ms 0

# Longer saturated run with more in-flight work
python3 scripts/stress-persistence.py --sessions 100 500 1000 --seconds 120 --concurrency 64 --think-ms 0
```

`--sessions` accepts 1–10,000 workers per phase, `--seconds` 1–600,
`--concurrency` 1–256, and `--think-ms` 0–60,000. All phases must exercise at least
one commit and rollback; a very short run with a long think time fails this check.
The configured connection pool limit follows `--concurrency`, but the report's
concurrency value is a limit on logical operations, not an observed connection count.

Choose a fresh output directory with `--output-dir /path/to/new-run`. Existing
output directories are refused so earlier measurements are not overwritten.

## Results and correctness

Results are written under `TestResults/persistence-stress/<UTC timestamp>-<id>/`:

| File | Contents |
| --- | --- |
| `report.json` | Per-phase throughput, operation counts, latency percentiles, errors and verification result |
| `postgres-stats.jsonl` | Timestamped Docker CPU, memory and block-I/O samples |
| `environment.json` | Image digest, resource limits, workload parameters and Git revision/dirty status |
| `build.log`, `test.log` | Build and test-host diagnostics |

Latency includes time waiting for a concurrency permit and the entire logical
operation, including transaction commit or rollback. Percentiles use nearest-rank
histograms rounded up to 1 ms; samples over 60 seconds share an overflow bucket
reported as the observed maximum. `AdmissionWait` measures waiting separately.
Operation percentiles include successful operations only. Errors are counted by
type, with deadline expiry reported as `timeout`; failed operations are not retried.

`ElapsedSeconds` includes draining accepted work and the last think delay.
`SuccessfulOperationsPerSecond` divides completed successful operations by that
interval. Process CPU, allocations and the end-of-phase working set cover the
whole .NET test host during that interval, not PostgreSQL. Each phase includes UTC start/end timestamps for correlation. Docker samples span
setup and verification as well as load; they are approximate and should not be
compared to per-phase latency without considering their timestamps.

After every phase, the test disposes the persistence service and initializes a
new instance against the same database. It verifies every session's final ID,
revision and payload; all committed journal revisions and counts; and the absence
of deliberately rolled-back journal rows. This is a persistence-instance reopen,
not a PostgreSQL restart, process crash or power-loss durability test.

The command exits unsuccessfully on workload errors, verification mismatches or
insufficient transaction coverage. JSON is written after each completed phase,
including a failed workload phase. Setup or test-host failures can occur before a
report exists; use the logs. There is no universal throughput/latency pass threshold:
compare repeated runs on the same host and workload before setting a performance
budget. The generator retains expected journal revisions for verification, so
long write-heavy runs also consume test-host memory.

## Ordinary tests and CI

The full stress test is marked `Category=Stress` and skipped unless
`MOONGATE_RUN_PERSISTENCE_STRESS=1`. The script sets that flag and filters to the
stress test explicitly. A four-session correctness smoke test (20 operations per session) and
histogram tests run with the ordinary persistence suite. No long stress run is
added to CI or triggered by a commit.

See [PostgreSQL persistence](persistence.md) for the application APIs and
[contributing](../CONTRIBUTING.md) for the ordinary test environment.
