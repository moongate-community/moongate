# Prepare a server root with mgboot

`mgboot` prepares a Moongate data directory without starting the server or connecting
to PostgreSQL. It ships beside `Moongate.Server` in release archives and Docker
images after 0.6.0, and the Linux installer links it as the `mgboot` command. It is
step 1 of [the first-start sequence](getting-started.md#first-start).

## Usage

```sh
mgboot /absolute/path/to/moongate-data
```

The root directory is the single required positional argument. Relative paths are
resolved from the current working directory. Quote paths containing spaces:

```sh
mgboot "/srv/my realm"
mgboot --help
```

On Windows, use `mgboot.exe C:\MoongateData` from the extracted distribution.
Keep `mgboot` and `Moongate.Server` from the same release together.

## What it creates

| Path under the root | Purpose |
| --- | --- |
| `config/moongate.toml` | Current server defaults serialized as snake_case TOML |
| `logs/`, `plugins/`, `scripts/` | Standard server directories |
| `migrations/auth/` | The core auth SQL files included in the distribution |
| `migrations/world/` | World migration directory; initially empty if no core World SQL is shipped |
| `.mgboot.lock` | Retained file used to prevent simultaneous initialization |

The new config sets `persistence.migrations_directory` to the absolute `migrations`
path inside this root. Normal server startup also defaults to `<root>/migrations`
when that setting is absent; keeping the explicit path makes the standalone
migration runner use the same catalog. Other values remain the server defaults: automatic migration
generation, automatic schema synchronization and the API listener are disabled.
The root does not need database access or Ultima Online client files to be prepared.

Base migration preparation copies the versioned SQL distributed with Moongate.
It does not generate new SQL from entities, load plugins, create databases or apply
migrations. Use the [persistence tools](persistence.md#automatic-development-migrations)
for entity changes after initialization.

## Existing directories

Running the command again preserves existing configuration and files. Missing
bundled migrations are added only when the existing catalog is compatible. An
existing migration with the same sequence number but a different filename or
checksum stops initialization before configuration or SQL files are written.
The error identifies the conflicting file. Do not delete migration history to
bypass it; reconcile the catalog or choose a new root.

An existing config is never rewritten, including its migration directory setting.
Check that setting yourself if it points outside this root. If you move the root,
update the absolute path in a newly generated config accordingly.

## Next steps

Continue with [Start a Moongate server](getting-started.md#first-start): edit the
generated configuration, create the PostgreSQL databases, apply the bundled SQL with
the migration runner and start the server. For development, you can instead enable
`persistence.auto_generate_migrations` after preparing the databases; see the
[entity tutorial](persistence-entity-tutorial.md).

## Docker

For an image built from source with mgboot support:

```sh
docker volume create moongate-data
docker run --rm --entrypoint /app/mgboot \
  --mount type=volume,source=moongate-data,target=/data \
  ghcr.io/moongate-community/moongate:latest /data
```

Choose a release containing mgboot or your locally built image. Initialization
exits after preparing the mounted root; it opens no TCP listener.

## Build from source

Publish the server and tool into the same directory:

```sh
dotnet publish src/Moongate.Server -c Release -r linux-x64 -o dist/moongate
dotnet publish src/Moongate.Boot -c Release -r linux-x64 -o dist/moongate
./dist/moongate/mgboot /absolute/path/to/moongate-data
```

Change the runtime identifier for your platform. The server also exposes the same
offline operation for source development:

```sh
dotnet run --project src/Moongate.Server -- --initialize-root --root-directory /absolute/path/to/moongate-data
```

`MOONGATE_SERVER_EXECUTABLE` can override the adjacent server executable for local
tooling tests. It must refer to a compatible executable from the same source version.
