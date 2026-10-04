# mgctl, the Moongate tool

`mgctl` is the one tool that ships beside `mgserver`, the server, in release archives and Docker
images; the Linux installer links it as the `mgctl` command. Releases up to 0.11 named the server `Moongate.Server`. Releases 0.7 to 0.11 named the tool
`mgboot`, which only prepared the root, and shipped the migrations and the converter as two more
executables, `migration-runner/Moongate.MigrationRunner` and `mg-uoxconv`; there, read
`mgboot <root>` for `mgctl init <root>`.

| Command | What it does |
| --- | --- |
| `mgctl init <root>` | Prepares a server root; this page. `mgctl <root>` does the same |
| `mgctl migrate status\|apply --target auth\|world` | Lists or applies the versioned SQL; see [Persistence migrations](persistence-migrations.md) |
| `mgctl convert uox ...` | Converts UOX3 `.dfn` content into TOML; see [Migrate from UOX3](uox3-migration.md) |
| `mgctl convert modernuo-spawns\|modernuo-signs\|modernuo-teleporters\|modernuo-locations\|modernuo-chests ...` | Converts ModernUO's spawners, signs, teleporters, named places and treasure chests; see [Migrate from UOX3](uox3-migration.md#signs-of-modernuo) |
| `mgctl completion bash\|zsh\|fish` | Prints the script that completes mgctl with TAB; see [TAB completion](#tab-completion) |

`mgctl --help` lists the commands and `mgctl <command> --help` the options of one.

## TAB completion

`mgctl completion <shell>` prints a completion script for bash, zsh or fish. With it, TAB
completes the commands (`mgctl mi` → `migrate`), the second word of `migrate` and `convert`,
the options of the command and what follows them: directories after `--root-directory` and
the like, files after `--source`, `auth` or `world` after `--target`, and a directory for
the root of `init`.

The [Linux installer](installation.md) puts the scripts where the shells look for them, so
a new shell completes mgctl with nothing to do. Elsewhere, load it yourself:

```sh
eval "$(mgctl completion bash)"      # bash: add the line to ~/.bashrc
source <(mgctl completion zsh)       # zsh: add the line to ~/.zshrc, after compinit
mgctl completion fish | source       # fish: or save it as ~/.config/fish/completions/mgctl.fish
```

The bash line uses `eval` because the bash 3.2 of macOS loads nothing from
`source <(...)`; there, a path with a space is completed without its quoting.

The fish script is generated from the same list of commands as the other two but, unlike
them, is not exercised by the tests.

## Prepare a server root

`mgctl init` prepares a Moongate data directory without starting the server or connecting
to PostgreSQL. It is step 1 of [the first-start sequence](getting-started.md#first-start).

## Usage

```sh
mgctl init /absolute/path/to/moongate-data
```

The root directory is the single required positional argument. Relative paths are
resolved from the current working directory. Quote paths containing spaces:

```sh
mgctl init "/srv/my realm"
mgctl init --help
```

`mgctl <root>`, without `init`, is the older spelling and still works for a root written as
a path (`/srv/moongate`, `./data`) or naming a directory that exists; a bare new name such
as `mgctl data` is refused, so a mistyped command never becomes a root. A command line
that names no command, such as `mgctl migrate` alone, exits with code 2.

On Windows, use `mgctl.exe init C:\MoongateData` from the extracted distribution.
Keep `mgctl` and `mgserver` from the same release together.
When preparing a root, `mgctl` shows the same Moongate banner, version and codename
as the server, followed by `Root setup`. Help and version output omit the banner.

## Generate an administration certificate

```sh
mgctl init /srv/moongate --generate-admin-certificate \
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
| `config/moongate.toml` | Current server defaults serialized as snake_case TOML; plugin sections such as `[ultima]` are appended at the first server start |
| `logs/`, `plugins/` | Standard server directories |
| `migrations/auth/` | The core auth SQL files included in the distribution |
| `migrations/world/` | The core World SQL files included in the distribution: the mobiles, items and world state tables |
| `data/` | The shard data files included in the distribution: maps, regions, races, skills, messages and the rest; see [Shard data files](data-files.md) |
| `templates/` | The item, loot and mobile templates included in the distribution; see [Templates](templates.md) |
| `scripts/` | The example [mobile](scripting/mobile-scripts.md) and [item scripts](scripting/item-scripts.md) included in the distribution, `mobiles/wander.lua` and `items/potion.lua`; the engine writes `definitions.lua` and `.luarc.json` here at startup |
| `.mgctl.lock` | Retained file used to prevent simultaneous initialization |

The new config sets `persistence.migrations_directory` to the absolute `migrations`
path inside this root. Normal server startup also defaults to `<root>/migrations`
when that setting is absent; keeping the explicit path makes
`mgctl migrate` use the same catalog. Other values remain the server defaults: automatic migration
generation and automatic schema synchronization are disabled. Runtime Redis
credentials must be supplied before starting the server.
The root does not need database access or Ultima Online client files to be prepared.

Data files, templates and scripts are copied only when missing, so a file you edited
stays as it is. Run `mgctl init` again after upgrading to add the files a new release
introduces; a file that exists in the root is never replaced, so compare it with the
one beside the new `mgserver` binary (`data/`, `templates/`, `scripts/`) to pick
up upstream changes. Nothing is removed either: a template a release renamed or moved
stays in the root beside its new copy, and the server stops at startup on the
duplicate id, so delete the stale file. A shipped file you deleted comes back on the
next run; empty it instead to keep it out.

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
`mgctl migrate apply` and start the server. For development, you can instead enable
`persistence.auto_generate_migrations` after preparing the databases; see the
[entity tutorial](persistence-entity-tutorial.md).

## Docker

For a release image:

```sh
docker volume create moongate-data
docker run --rm --entrypoint /app/mgctl \
  --mount type=volume,source=moongate-data,target=/data \
  ghcr.io/moongate-community/moongate:latest init /data
```

Images up to 0.11.0 have `/app/mgboot` instead: use `--entrypoint /app/mgboot` and pass
only `/data`. Initialization exits after preparing the mounted root; it opens no TCP
listener.

## Build from source

Publish the server and tool into the same directory:

```sh
dotnet publish src/Moongate.Server -c Release -r linux-x64 -o dist/moongate
dotnet publish src/Moongate.Ctl -c Release -r linux-x64 -o dist/moongate
./dist/moongate/mgctl init /absolute/path/to/moongate-data
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
