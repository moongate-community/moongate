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

## Generate an administration certificate

```sh
mgboot /srv/moongate --generate-admin-certificate \
  --admin-certificate-hosts "login.example.test,192.0.2.10"
```

The CLI uses ConsoleAppFramework. `--generate-admin-certificate` is an optional
presence flag. `--admin-certificate-hosts` is one comma-separated string and
requires that flag. Omit the hosts option for local development: every certificate
includes `localhost`, `127.0.0.1` and `::1` automatically. Additional entries must
be DNS names or IP addresses, without URL schemes, ports or wildcard addresses.
Use the names clients connect to; `*`, `0.0.0.0` and `::` are bind addresses, not
certificate identities.

The operation runs offline and creates a self-signed RSA server certificate valid
for one year. It writes these files under the root:

| File | Contents |
| --- | --- |
| `certificates/admin.pfx` | Server certificate and private key; no password; owner read/write only on Unix |
| `certificates/admin.crt` | Public PEM certificate to trust in administration clients |

It updates these four settings in `config/moongate.toml`:

```toml
[admin_api]
enabled = true
allow_insecure_loopback = false
certificate_path = "certificates/admin.pfx"
certificate_password = ""
```

The existing port, listen address, other settings and comments are preserved.
A missing section is added; existing inline or dotted `admin_api` definitions must
first be converted to an explicit `[admin_api]` section. A custom certificate path
is never replaced implicitly: clear that setting explicitly only if you intend
to switch identities.

No port opens during setup. The API starts at the next normal server startup.
The default bind remains `127.0.0.1:2590`; for private-network access, configure a
reachable interface or `listen_address = "*"` and include the actual client-facing
DNS/IP in the certificate hosts. See [Administration API](admin-api.md).

Rerunning the same command reuses a matching, unexpired certificate pair. It fails
without overwriting that pair if either file is missing, invalid, expired,
mismatched or lacks a requested name. There is no implicit renewal or rotation.
For deliberate replacement, stop the server, move both existing certificate files
to a protected backup location, rerun generation and update client trust before
restarting. A passwordless PFX still contains the private key: keep it server-side
and restrict access. On Windows, use an appropriate ACL for the service account.
Distribute only `admin.crt`; clients must verify trust and hostname.

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
generation and automatic schema synchronization are disabled. Runtime Redis
credentials must be supplied before starting the server.
The root does not need database access or Ultima Online client files to be prepared.

Base migration preparation copies the versioned SQL distributed with Moongate.
It does not generate new SQL from entities, load plugins, create databases or apply
migrations. Use the [persistence tools](persistence-migrations.md#automatic-development-migrations)
for entity changes after initialization.

## Existing directories

Running the command again without certificate options preserves existing configuration and files. Missing
bundled migrations are added only when the existing catalog is compatible. An
existing migration with the same sequence number but a different filename or
checksum stops initialization before configuration or SQL files are written.
The error identifies the conflicting file. Do not delete migration history to
bypass it; reconcile the catalog or choose a new root.

Without `--generate-admin-certificate`, an existing config is never rewritten, including its migration directory setting.
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

The same certificate options work with the server's offline operation:

```sh
dotnet run --project src/Moongate.Server -- --initialize-root \
  --root-directory /srv/moongate --generate-admin-certificate \
  --admin-certificate-hosts "login.example.test,192.0.2.10"
```

The server rejects certificate options outside `--initialize-root`.

`MOONGATE_SERVER_EXECUTABLE` can override the adjacent server executable for local
tooling tests. It must refer to a compatible executable from the same source version.
