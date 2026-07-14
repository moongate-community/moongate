# Installation

## Requirements

- The .NET SDK selected by the repository is **10.0.301**, with `latestPatch` roll-forward and prerelease SDKs disabled.
- Projects target **.NET 10** (`net10.0`). The server project builds an executable and directly references the Network, Persistence, and Scripting projects. Its package dependencies include ConsoleAppFramework 5.7.13 and SquidStd 0.33.1 components.
- A separately obtained Ultima Online client-data directory is required for meaningful asset loading and client use. Moongate does not supply those proprietary client files.

The repository files establish the .NET requirement and a Linux container build. They do not establish a broader host-operating-system support matrix.

## Build from source

From a checkout of the repository:

```bash
dotnet restore Moongate.slnx
dotnet build Moongate.slnx --configuration Release --no-restore
```

The executable project is `src/Moongate.Server/Moongate.Server.csproj`. To confirm the currently bound command-line options:

```bash
dotnet run --project src/Moongate.Server/Moongate.Server.csproj -- --help
```

The verified help output lists `--root-directory`, `--show-header`, and `--uo-directory`.

## Build the container image

The repository Dockerfile uses the .NET 10 SDK to build a framework-dependent Linux x64 single-file executable, then runs it on the .NET 10 runtime image:

```bash
docker build -f src/Moongate.Server/Dockerfile -t moongate:local .
```

The image exposes TCP port 2593 and starts `./Moongate.Server`. The Dockerfile alone does not define a complete production deployment recipe: it does not, for example, supply Ultima client data or define the required mounts and published ports.

Continue to [Configuration](./configuration.md).
