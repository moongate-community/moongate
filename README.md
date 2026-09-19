<p align="center">
  <img src="images/moongate_logo.png" alt="Moongate logo" width="220" />
</p>

<h1 align="center">Moongate</h1>

<p align="center">
  <a href="https://github.com/moongate-community/moongate/actions/workflows/ci.yml"><img src="https://github.com/moongate-community/moongate/actions/workflows/ci.yml/badge.svg?branch=develop" alt="CI"></a>
  <a href="https://github.com/moongate-community/moongate/actions/workflows/security.yml"><img src="https://github.com/moongate-community/moongate/actions/workflows/security.yml/badge.svg?branch=main" alt="Security Audit"></a>
  <a href="https://github.com/moongate-community/moongate/pkgs/container/moongate"><img src="https://img.shields.io/badge/ghcr.io-moongate-2496ED?logo=docker&logoColor=white" alt="Container image"></a>
  <img src="https://img.shields.io/badge/platform-.NET%2010-blueviolet" alt=".NET 10">
  <img src="https://img.shields.io/badge/license-AGPL--3.0--or--later-blue" alt="AGPL-3.0-or-later">
</p>

## Docker

Every release publishes a `linux/amd64` image to the GitHub Container Registry,
tagged with the version and with `latest`.

```bash
docker pull ghcr.io/moongate-community/moongate:latest
```

The server listens on port 2593 and keeps its configuration, logs, plugins and
saves under `/data`. Mount a volume there so that state outlives the container.

```bash
docker run -d --name moongate \
  -v moongate-data:/data \
  -p 2593:2593 \
  ghcr.io/moongate-community/moongate:latest
```

The first start writes `/data/config/moongate.toml` and then exits, because
`ultima_path` still holds its placeholder. Mount your Ultima Online client files
and point that setting at them:

```toml
[ultima]
ultima_path = "/uo"
```

```bash
docker run -d --name moongate \
  -v moongate-data:/data \
  -v /path/to/ultima:/uo:ro \
  -p 2593:2593 \
  ghcr.io/moongate-community/moongate:latest
```

`MOONGATE_ROOT` and `--root-directory` move that root elsewhere. Run several
shards from the same image by giving each container its own volume and its own
published port; a root is meant for one server at a time.

[Diagnostics](docs/diagnostics.md)

[Dependency security audit](docs/security-audit.md)

## Server mode

Set `mode` at the root of `config/moongate.toml`, before any table headers:

```toml
mode = "standalone"
```

Supported values are `"login"`, `"game"`, and `"standalone"`. Omitting the setting
defaults to standalone. In C#, `MoongateServerConfig.Mode` uses the `ServerMode`
flags enum, where `Standalone = Login | Game`; an empty or unknown mode is rejected.

This setting currently defines the configuration contract. It does not yet select
which services start; separate login and game runtimes will use it in a subsequent change.

## Libraries

The seven library packages have their own English READMEs and runnable examples.
See [NuGet libraries and package verification](docs/nuget-packaging.md) for the
package list, dependencies, and the local verification command.
