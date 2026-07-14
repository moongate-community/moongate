# Operations

::: danger Predictable administrator credentials
At persistence initialization, the current seeder upserts an active administrator account with username `admin` and password `admin`, then logs both credentials at warning level and saves a snapshot. Do not expose the server to a public or untrusted network while this predictable account is enabled. This repository currently implements and documents no operator procedure for changing or disabling these credentials.
:::

## Start the server

From the repository root, start Moongate with a writable root and an existing Ultima client-data directory:

```bash
dotnet run --project src/Moongate.Server/Moongate.Server.csproj -- \
  --root-directory ./moongate_root \
  --uo-directory /absolute/path/to/ultima-client
```

The verified command-line options are `--root-directory`, `--uo-directory`, and `--show-header <bool>`. See [Configuration](./configuration.md) for their defaults and the `MOONGATE_ROOT` fallback.

## Recognize a successful startup

Startup configures logging and plugins, then starts the registered lifecycle services. Use these exact message templates as checkpoints:

- `UO client files located in {Directory} ({FileCount} files)` means the Ultima file locator was pointed at `UltimaDirectory` and counted its non-empty resolved paths. Inspect the rendered directory and count; this message does not validate a complete client-data installation.
- `Executed {Count} data loader(s)` appears only after every registered data loader completes.
- `Network service listening on {Address}:{Port}` means the TCP server successfully bound `Network.Address` and `Network.Port`. The rendered port is the actual bound port.
- `Initializing default timers...` followed by `Default timers initialized.` means the engine-started handler registered the recurring persistence timer.

The network service also reports `Total packet table: {PacketCount}`, `Total packets info: {PacketCount}`, and `Registering {Count} packet handlers` before it creates the listener.

## Persistence snapshots

After the engine starts, Moongate registers `persistence_save` at a 300-second interval. Each timer invocation logs `Start saving snapshot...`, calls the persistence service, and, after that call completes, logs `Snapshot saved in {ElapsedMilliseconds} milliseconds.` The implementation passes the `TimeSpan` returned by `Stopwatch.GetElapsedTime(start)` into `{ElapsedMilliseconds}`. The rendered duration is therefore mislabeled by the template and must not be interpreted as a numeric millisecond value.

The persistence plugin stores snapshots under the root's registered `saves` directory. Its initial seeder also saves a snapshot after creating the default account. These are implemented persistence writes; the repository does not define operator backup, restore, or upgrade procedures.

## Network addresses

`Network.Address` is the local bind address. `Network.PublicAddress` is placed in the server-list and game-server redirect packets, while `Network.Port` is used for both the listener and redirect.

For local-only clients, loopback values can work. For remote clients, bind to an appropriate local interface and set `PublicAddress` to an IP address those clients can reach. Both address settings are parsed as IP literals; hostnames are not resolved. A listener log proves the bind succeeded, but it does not prove the advertised address is reachable from a client.

## Stop the server

Press Ctrl+C to request cancellation. The entry point passes the command cancellation token to the SquidStd run loop. During network-service shutdown, Moongate stops and disposes the TCP server and clears its listener reference. Calling network disposal follows the same stop path, and stopping before the listener exists is a no-op.

The file-loader and data-loader services have no shutdown work. The current lifecycle code does not explicitly request a final persistence snapshot during shutdown, so do not treat cancellation as a documented save-on-exit operation.

Continue to [Troubleshooting](./troubleshooting.md), or review [Current limitations](./limitations.md).
