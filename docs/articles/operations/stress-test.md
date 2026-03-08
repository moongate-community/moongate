# Stress Test

`tools/Moongate.Stress` is a black-box load testing tool that spawns real UO TCP clients against a live Moongate server. It validates login flow, enters the game world, runs a continuous movement loop, and evaluates Service Level Objectives (SLOs).

## Test Flow

The test runs in five phases:

### Phase 1: User Bootstrap

The tool ensures test accounts exist before connecting. It calls the HTTP API to create accounts with format `{prefix}_{index:0000}` (e.g. `stress_0001`, `stress_0002`, ...).

- Fetches existing users via `GET /api/users/`
- Creates missing accounts via `POST /api/users/`
- If JWT is enabled, authenticates first via `POST /auth/login` with admin credentials

### Phase 2: Ramp-up

Clients spawn gradually at the configured rate (`--ramp-up-per-second`). The delay between each client spawn is `1.0 / ramp-up-per-second` seconds. The duration timer starts after all clients are launched.

### Phase 3: Login

Each client executes the full UO login handshake:

1. Connects to the login server (`host:port`)
2. Sends login seed packet (`0xEF`)
3. Sends account login packet (`0x80`) with username/password
4. Receives server list (`0xA8`), sends server select (`0xA0`, index 0)
5. Receives redirect packet (`0x8C`) with game server IP, port, and session key

### Phase 4: Game Entry

After redirect, the client connects to the game server:

1. Sends seed packet with session key
2. Sends game login packet (`0x91`) with session key, username, password
3. Sends login character packet (`0x5D`)
4. If no login confirmation within 7 seconds, falls back to character creation (`0xF8`)
5. Waits for login confirmation (`0x1B`) within a 10-second deadline
6. On failure, marks login as failed

### Phase 5: Movement Loop

Each logged-in client enters a continuous movement loop until the test duration expires:

1. Sends a random directional movement packet (`0x02`) with a running flag and incrementing sequence number
2. Waits up to 1500ms for the movement ACK packet (`0x22`) matching the sequence
3. Records the ACK latency using `Stopwatch.GetTimestamp()` precision timing
4. Waits `--move-interval-ms` then sends the next move

## CLI Options

| Flag | Default | Description |
|------|---------|-------------|
| `--host` | `127.0.0.1` | Server hostname |
| `--port` | `2593` | Login server port (1-65535) |
| `--http` | `http://localhost:8088` | HTTP API base URL for user bootstrap |
| `--clients` | `100` | Number of concurrent stress clients (1-10,000) |
| `--duration` | `300` | Test duration in seconds (10-86,400) |
| `--ramp-up-per-second` | `10` | Clients to spawn per second (1-10,000) |
| `--move-interval-ms` | `300` | Delay between moves per client in ms (50-10,000) |
| `--user-prefix` | `stress` | Username prefix for test accounts |
| `--user-password` | `StressPwd#123` | Password for all test accounts |
| `--user-role` | `Regular` | Account role (Regular, Counselor, GameMaster, Administrator) |
| `--admin-username` | _(none)_ | Admin username for JWT authentication |
| `--admin-password` | _(none)_ | Admin password for JWT authentication |
| `--verbose` | `false` | Enable verbose per-client logging |

## Run

Quick local test with 10 clients for 30 seconds:

```bash
dotnet run --project tools/Moongate.Stress -- \
  --host 127.0.0.1 --port 2593 \
  --http http://localhost:8088 \
  --clients 10 --duration 30 --ramp-up-per-second 5
```

Full 100-client run:

```bash
dotnet run --project tools/Moongate.Stress -- \
  --host 127.0.0.1 --port 2593 \
  --http http://localhost:8088 \
  --clients 100 --duration 300 --ramp-up-per-second 10
```

With JWT-enabled HTTP API:

```bash
dotnet run --project tools/Moongate.Stress -- \
  --host 127.0.0.1 --port 2593 \
  --http http://localhost:8088 \
  --clients 100 --duration 300 \
  --admin-username admin --admin-password your_password
```

## Service Level Objectives (SLOs)

The tool evaluates these SLOs at the end of the run. The process exits with code `0` on pass, `1` on failure.

| SLO | Threshold |
|-----|-----------|
| Login success rate | >= 99% |
| Unexpected disconnects | 0 |
| Movement ACK p95 latency | < 200ms |
| Minimum ACKs received | >= 1 |

All conditions must pass for the test to succeed.

## Metrics Collected

| Metric | Description |
|--------|-------------|
| `totalClients` | Number of configured clients |
| `loginSucceeded` | Successful login count |
| `loginFailed` | Failed login count |
| `unexpectedDisconnects` | Premature disconnection count |
| `movesSent` | Total movement packets sent |
| `movesAcked` | Total movement ACK packets received |
| `ackLatencyP50Ms` | 50th percentile ACK latency (ms) |
| `ackLatencyP95Ms` | 95th percentile ACK latency (ms) |
| `ackLatencyP99Ms` | 99th percentile ACK latency (ms) |
| `durationSeconds` | Actual test duration |

Latency is measured per-packet using `Stopwatch.GetTimestamp()` for high-precision timing. Percentiles are computed from the full sorted latency distribution at the end of the run.

## Output

The tool produces two outputs:

**Console summary**: prints all metrics, SLO evaluation results, and pass/fail status.

**JSON artifact**: written to `artifacts/stress/latest.json`:

```json
{
  "metrics": {
    "totalClients": 100,
    "loginSucceeded": 100,
    "loginFailed": 0,
    "unexpectedDisconnects": 0,
    "movesSent": 98200,
    "movesAcked": 98195,
    "ackLatencyP50Ms": 2.1,
    "ackLatencyP95Ms": 8.4,
    "ackLatencyP99Ms": 15.2,
    "durationSeconds": 300
  },
  "passed": true,
  "failedConditions": []
}
```

## Implementation Details

The stress client speaks real UO protocol:

- **Packet writers** for all login/movement opcodes (`0x80`, `0x91`, `0xEF`, `0x5D`, `0xF8`, `0x02`, `0xA0`)
- **Huffman decompression** for server responses (256-entry encoding table with variable-length codes)
- **Character creation fallback**: if no existing character is found, the client creates a default male human on Trammel with base stats (STR 60, DEX 50, INT 40)
- **Thread-safe metrics**: uses `Interlocked` operations, `ConcurrentDictionary`, and `ConcurrentQueue` for lock-free collection

## Notes

- This is a practical operational stress test, not a micro-benchmark.
- Run on the same network profile you want to validate (localhost, LAN, or staging).
- Start with short runs (`--clients 10 --duration 30`) before scaling to 100+ clients.
- The tool does not test game logic beyond movement. It validates server stability under concurrent connection and packet load.

---

**Previous**: [Configuration](../getting-started/configuration.md) | **Next**: [Console Commands](console-commands.md)
