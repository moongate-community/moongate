# First launch

::: danger Predictable administrator credentials
At persistence initialization, the current seeder upserts an active administrator account with username `admin` and password `admin`, then logs both credentials at warning level and saves a snapshot. Do not expose the server to a public or untrusted network while this predictable account is enabled. This repository currently implements and documents no operator procedure for changing or disabling these credentials.
:::

## Run from source

From the repository root, choose a writable server root and point the server at an existing Ultima Online client-data directory:

```bash
dotnet run --project src/Moongate.Server/Moongate.Server.csproj -- \
  --root-directory ./moongate_root \
  --uo-directory /absolute/path/to/ultima-client
```

The option spellings were verified with:

```bash
dotnet run --project src/Moongate.Server/Moongate.Server.csproj -- --help
```

The server also exposes `--show-header <bool>` in help, with a default of `true`. Press Ctrl+C to request shutdown; ConsoleAppFramework supplies the cancellation token that the entry point passes into the SquidStd run loop.

::: warning Current limitation
The launch probe used an empty temporary directory to exercise service startup and graceful cancellation. It did not exercise proprietary Ultima client data or a real client connection. Supply client files you are entitled to use; a startup log is not evidence that a client can connect successfully.
:::

## What startup does

At a high level, the current entry point:

1. resolves the root and Ultima directories, loads configuration, and applies the Ultima CLI override;
2. configures logging and loads the persistence, scripting, and data-loader plugins;
3. registers lifecycle services, packet handlers, the event loop, timers, jobs, and event delivery;
4. starts persistence and the TCP listener, initializes Lua, locates Ultima files, and runs the bundled YAML data loaders;
5. initializes default timers after the engine-started event.

A fresh writable root is populated during startup, including `moongate.yaml` and bundled data, template, and scripting files. Treat the displayed “started” message and listener log as service-startup evidence only.

## Concrete startup failures

- An empty `UltimaDirectory` after configuration and CLI processing raises `UODirectoryNotValidException`.
- `Network.Address` is parsed as an IP literal before binding; an invalid value fails parsing, while an unavailable address or port can prevent the listener from starting.
- The root must be usable by services that create configuration, persistence, data, template, and script files.
- Client-data-dependent behavior cannot be validated without a suitable external Ultima data set, even if the file-loader stage reports the selected directory.

Continue to [Operations](./operations.md), or return to the [Server guide](./index.md).
