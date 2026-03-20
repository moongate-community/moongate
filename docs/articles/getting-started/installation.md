# Installation Guide

Install and run Moongate v2 with the current runtime requirements.

## Requirements

- .NET SDK 10.0.x
- Git
- Ultima Online data directory (path passed with `--uo-directory` or `MOONGATE_UO_DIRECTORY`)

## Clone And Build

```bash
git clone https://github.com/moongate-community/moongate.git
cd moongate
dotnet restore
dotnet build -c Release
```

## Run (Local)

```bash
dotnet run --project src/Moongate.Server -- \
  --root-directory ./moongate \
  --uo-directory /path/to/uo \
  --loglevel Information
```

Equivalent with environment variable:

```bash
export MOONGATE_UO_DIRECTORY=/path/to/uo
dotnet run --project src/Moongate.Server
```

## Publish (Optional)

Example for a release publish:

```bash
dotnet publish src/Moongate.Server/Moongate.Server.csproj \
  -c Release \
  -o ./artifacts/publish
```

## Docker

Build:

```bash
./scripts/build_image.sh -t moongate-server:local
```

Run:

```bash
docker run --rm -it \
  -p 2593:2593 \
  -p 8088:8088 \
  -v /path/to/moongate-data:/app \
  -v /path/to/uo:/uo:ro \
  moongate-server:local
```

The Docker image builds and bundles the frontend (`ui/`) and serves it at:

- `http://localhost:8088/`

Recommended env vars in container:

- `MOONGATE_ROOT_DIRECTORY=/app`
- `MOONGATE_UO_DIRECTORY=/uo`
- `MOONGATE_IS_DOCKER=true`
- `MOONGATE_UI_DIST=/opt/moongate/ui/dist`

## Verify Startup

```bash
curl http://localhost:8088/health
```

Expected response:

```text
ok
```

Metrics endpoint:

```bash
curl http://localhost:8088/metrics
```

If metrics are enabled/configured, response is Prometheus text format.

## Configuration File

If `<root>/moongate.json` does not exist, server generates one.

Current top-level model includes:

- `rootDirectory`
- `uoDirectory`
- `logLevel`
- `logPacketData`
- `isDeveloperMode`
- `http`
- `game`
- `metrics`
- `persistence`

Bootstrap now binds the full config model from `moongate.json` and applies `MOONGATE_*` environment overrides on top.

---

**Previous**: [Quick Start](quickstart.md) | **Next**: [Configuration Guide](configuration.md)
