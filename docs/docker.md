# Run with Docker

Moongate publishes Linux images to [GitHub Container Registry](https://github.com/moongate-community/moongate/pkgs/container/moongate). Use the documentation for the version you run; the [changelog](../CHANGELOG.md) identifies published releases. To run the current source, build the image locally:

```sh
docker build -f src/Moongate.Server/Dockerfile -t moongate:local .
```

The image runs as a non-root user with `MOONGATE_ROOT=/data`. Mount a persistent writable volume there and mount your own Ultima Online client files read-only; client files are not distributed with Moongate. It ships `mgboot`, the migration runner, the core SQL and `mg-uoxconv`. The `sample-plugin` build target adds the sample plugin bundle.

## Build cache

The Dockerfile restores dependencies before copying source files, then publishes each bundled
executable with `--no-restore`. Restore and publish use the same configuration, target architecture
and self-contained setting. There is no separate server build before publication.

The build context includes source, core migrations, build settings, licenses and the sample plugin.
Documentation, tests, the website, local `bin`/`obj` directories and environment files are excluded.
Sample plugin source is copied only into its own build target. When adding a new image input,
update `.dockerignore` and the relevant `COPY` instructions together.

Development and release workflows export intermediate layers to GHCR under `buildcache-develop`
and `buildcache-release`. Each workflow reads both caches but writes only its own tag. These tags
contain build cache, not runnable server images. NuGet packages remain in the restore layer so
fresh CI builders can recover them from the external cache. A first build without a cache still
works normally.

For a local build that also reads the development cache:

```sh
docker buildx build --load \
  --cache-from type=registry,ref=ghcr.io/moongate-community/moongate:buildcache-develop \
  -f src/Moongate.Server/Dockerfile -t moongate:local .
```

Repeat the build with `--progress=plain` to inspect cached steps. Documentation-only changes
should leave restore and publish layers cached. Use `--pull` to check for updated .NET base
images, or `--no-cache` to rebuild all steps. Published images remain Linux amd64.

## Recommended Compose example

The [login and two game instances example](docker-login-realms.md) is a complete build-from-source deployment with:

- A login process on UO TCP port 2593 and two game processes on host ports 2595 and 2596.
- Separate Accounts, Realm 1 and Realm 2 PostgreSQL databases and schema/runtime roles.
- One private Redis service for live realm leases and one-use login handoff tickets.
- Bitwarden-sourced Compose secrets, schema jobs, persistent server/PostgreSQL volumes and a disposable smoke test.

Follow that guide for the exact `.env`, secret exports, build, SQL application and startup commands. Redis is not published to the host. There is no internal API listener or certificate exchange in this topology.

## Standalone container

`mode = "standalone"` runs both UO roles in one process, with separate login and game listeners. Publish both configured ports. Standalone also needs reachable Accounts and Realm PostgreSQL databases and the shared Redis service; it advertises its own local realm through Redis. Its root TOML includes:

```toml
mode = "standalone"

[network]
login_port = 2593
game_port = 2595

[ultima]
ultima_path = "/uo"

[persistence.accounts]
connection_string = "$MOONGATE_ACCOUNTS_DATABASE"

[persistence.realm]
connection_string = "$MOONGATE_REALM_DATABASE"

[redis]
connection_string = "$MOONGATE_REDIS_CONNECTION_STRING"
handoff_secret = "$MOONGATE_HANDOFF_SECRET"
```

Supply the four referenced variables from a secret provider in the container environment. PostgreSQL variables are `postgres://` URIs for role-specific databases. The Redis variable is a StackExchange.Redis connection string such as `redis:6379,password=<secret>` on a private Docker network; the handoff secret is a different value. Do not commit either value into TOML, Compose or `.env`. The [configuration reference](server-configuration.md) covers the other settings.

Prepare a root before starting, then apply Auth and World SQL to their respective databases:

```sh
docker volume create moongate-data
docker run --rm --entrypoint /app/mgboot -v moongate-data:/data moongate:local /data
```

For optional administration TLS in this root, append `--generate-admin-certificate`
after `/data`, with `--admin-certificate-hosts` naming the DNS/IP used by clients.
See [mgboot certificate setup](mgboot.md#generate-an-administration-certificate)
for the generated files and client trust. The default bind remains loopback;
configure a private interface before connecting from another container. The
multi-process Compose override instead uses its mounted TOMLs and operator-provided
certificates, as described in the [example README](../examples/docker/login-realms/README.md#optional-administration-api).

Mount that same volume for the server and migration runner. Stop the affected runtime before applying new reviewed SQL. For the runner's targets and output, see [Generate, review and apply](persistence-migrations.md#generate-review-and-apply). A missing database, Redis connection or required migration fails startup; the server does not create databases or apply unreviewed SQL automatically.

## Ports, storage and updates

The current image declares UO client ports 2593 and 2595. `EXPOSE` does not publish a host port; configure `ports` for the login and each game listener that clients must reach. Keep Redis and PostgreSQL on a private network. Each running Moongate process needs its own `/data` volume; each realm needs its own Realm database. Do not share one root or Realm database between running game processes.

`docker compose down` preserves named volumes. Adding `--volumes` deletes server roots and PostgreSQL data; use it only for a disposable environment. World saves are not PostgreSQL backups. Stop services normally so the final world save can complete.

To update an instance, read the target release's changelog, stop it, follow your database backup policy, change the image tag, and restart with the same volumes. If startup reports pending migrations, stop the affected server and apply the reviewed SQL before starting it again.

## UOX3 content conversion

The image includes `/app/mg-uoxconv` for converting UOX3 `.dfn` files to TOML. Bind the source read-only and an output directory writable by the invoking user:

```sh
docker run --rm --entrypoint /app/mg-uoxconv \
  --user "$(id -u):$(id -g)" \
  -v /path/to/uox3/dfndata/items:/uox-source:ro \
  -v /path/to/templates:/uox-out \
  moongate:local \
  --source /uox-source --destination /uox-out/items --loot-destination /uox-out/loots
```

See [Migrate from UOX3](uox3-migration.md) for what the converter does and its current limits.

## Troubleshooting

- **Database connection fails:** `localhost` inside a container means that container. Use the reachable service name or private-network address and confirm the role has access to its target database.
- **Redis connection fails:** verify the private service, its health, the shared credential and `noeviction` policy. A Redis restart clears tickets and leases; games republish leases after reconnect.
- **Realm absent from login:** check the game logs and Redis lease TTLs as shown in the [Compose guide](docker-login-realms.md#start-observe-and-stop).
- **Client redirect cannot connect:** the game's advertised IPv4 address and port must be reachable from the client and match the published host mapping.
- **Missing client data or unwritable root:** check the `/uo` read-only mount and `/data` ownership for the image's non-root user.

See [First start](getting-started.md), [Configuration](server-configuration.md) and [Diagnostics](diagnostics.md) for server-level operation.

## Private administration endpoint

Port 2590 is reserved for optional gRPC administration and remains disabled in the default image/Compose configuration. Use server TLS on the private network; no mTLS or public port mapping is required. See the [administration guide](admin-api.md) and [opt-in Compose configuration](../examples/docker/login-realms/README.md#optional-administration-api).
