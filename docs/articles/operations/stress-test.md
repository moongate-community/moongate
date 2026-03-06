# Stress Test (Socket UO, Black-Box)

This guide explains how to run the built-in stress runner against a live Moongate server.

## What it does

`tools/Moongate.Stress` creates a black-box load using real TCP socket clients that speak the UO protocol:

1. Ensures test users exist (`stress_0001 ... stress_0100`) via HTTP `/api/users`.
2. Opens UO socket connections to game port `2593`.
3. Executes login flow and enters world.
4. Runs continuous movement loop and measures movement ACK latency.
5. Evaluates SLOs and exits with `0` (pass) or `1` (fail).

## Default SLOs

- Login success rate `>= 99%`
- Unexpected disconnects `= 0`
- Movement ACK p95 `< 200ms`

## Run

```bash
dotnet run --project tools/Moongate.Stress -- \
  --host 127.0.0.1 --port 2593 \
  --http http://localhost:8088 \
  --clients 100 --duration 300 --ramp-up-per-second 10
```

## JWT-enabled HTTP API

If `/api/users` is protected, provide admin credentials so the runner can authenticate at `/auth/login`:

```bash
dotnet run --project tools/Moongate.Stress -- \
  --admin-username admin --admin-password your_password
```

## Output

- Console summary with counters and percentiles.
- JSON artifact: `artifacts/stress/latest.json`.

## Notes

- This is a practical operational stress test, not a micro-benchmark.
- Run on the same network profile you want to validate (local host, LAN, or staging).
- Start with short runs (`--clients 10 --duration 30`) before 100-client runs.
