# Run with Docker

Each Moongate release publishes a **linux/amd64** image to
[GitHub Container Registry](https://github.com/moongate-community/moongate/pkgs/container/moongate).
Images are tagged with the version (without `v`) and with `latest`. The examples
below pin `0.6.0`; choose the version you want from the [changelog](../CHANGELOG.md)
or [GitHub releases](https://github.com/moongate-community/moongate/releases).
The site header displays the current version.

You need Docker with Compose v2 and your own Ultima Online client files; the image
does not include them. The server also needs two PostgreSQL databases, which the
image does not bundle either: the Compose file below adds one. TCP port **2593** is
the game listener; **2594/tcp** is declared for the optional internal API, which is
disabled by default.

The image sets `MOONGATE_ROOT=/data` and runs as the .NET image's non-root user.
Beside the server it ships the core SQL at `/app/migrations` and the migration runner
at `/app/migration-runner/Moongate.MigrationRunner`. Images after 0.6.0 also ship
`/app/mgboot` and `/app/mg-uoxconv`.

## First start

These are the steps of [Start a Moongate server](getting-started.md#first-start),
run through Compose. Save the two files in an empty directory, replacing the client
path:

```yaml
# compose.yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_USER: moongate
      POSTGRES_PASSWORD: moongate
      POSTGRES_DB: auth
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./create-world.sql:/docker-entrypoint-initdb.d/create-world.sql:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U moongate -d auth"]
      interval: 5s
      timeout: 3s
      retries: 10

  moongate:
    image: ghcr.io/moongate-community/moongate:0.6.0
    depends_on:
      postgres:
        condition: service_healthy
    ports:
      - "2593:2593"
    volumes:
      - moongate-data:/data
      - /absolute/path/to/ultima:/uo:ro

volumes:
  moongate-data:
  postgres-data:
```

```sql
-- create-world.sql
CREATE DATABASE world OWNER moongate;
```

The PostgreSQL image creates the `auth` database and the `moongate` role from its
environment and runs `create-world.sql` once, on an empty `postgres-data` volume.
These are development credentials; for anything else, see
[PostgreSQL persistence](#postgresql-persistence) below.

1. **Prepare the root.** Start the stack once:

   ```sh
   docker compose up -d
   ```

   PostgreSQL stays up. The server writes `/data/config/moongate.toml` into the
   `moongate-data` volume and exits, because `ultima_path` still holds its
   placeholder. Images after 0.6.0 can do this without a server start:
   `docker compose run --rm --entrypoint /app/mgboot moongate /data`.

2. **Edit the configuration.** Copy it out, edit it, copy it back:

   ```sh
   docker compose cp moongate:/data/config/moongate.toml ./moongate.toml
   ```

   ```toml
   [ultima]
   ultima_path = "/uo"

   [persistence]
   auto_sync_schema = false
   migrations_directory = "/app/migrations"

   [persistence.accounts]
   connection_string = "postgres://moongate:moongate@postgres:5432/auth"

   [persistence.realm]
   connection_string = "postgres://moongate:moongate@postgres:5432/world"
   ```

   Both paths are **inside the container**: `/uo` is the client mount, and
   `/app/migrations` is the core SQL shipped in the image. The database host is the
   Compose service name; `localhost` inside the container is the container itself.
   With a root prepared by `mgboot`, leave `migrations_directory` as generated. Keep
   the other sections, then:

   ```sh
   docker compose cp ./moongate.toml moongate:/data/config/moongate.toml
   ```

3. **Apply the core migrations.** The server refuses to start while files are pending:

   ```sh
   docker compose run --rm --entrypoint /app/migration-runner/Moongate.MigrationRunner \
     moongate apply --root-directory /data --target auth
   docker compose run --rm --entrypoint /app/migration-runner/Moongate.MigrationRunner \
     moongate apply --root-directory /data --target world
   ```

4. **Start the server.**

   ```sh
   docker compose up -d
   docker compose logs --tail 100 -f moongate
   ```

   A successful start logs `Postgres connection successful` twice, then the bound
   endpoints. Stop the log viewer with Ctrl+C; it does not stop the container. Use
   `docker compose stop` for a normal shutdown and `docker compose start` to resume.

Compose creates project-scoped named volumes. Keep the same project name and
directory when restarting the same world. `docker compose down` removes the
containers but retains the volumes; adding `--volumes` deletes the server root
**and the databases**.

## Plain docker run

Without Compose, the same steps work against a PostgreSQL server you already run.
Create persistent storage, then start once to write the configuration:

```sh
docker volume create moongate-data
docker run -d --name moongate \
  --mount type=volume,source=moongate-data,target=/data \
  --mount type=bind,source=/absolute/path/to/ultima,target=/uo,readonly \
  -p 2593:2593 \
  ghcr.io/moongate-community/moongate:0.6.0
```

Edit the configuration as in step 2 above, with `docker cp` in place of
`docker compose cp`, and point both connection strings at a host the container can
reach. Apply the migrations with the same entrypoint through `docker run --rm` and
the same volume mount, then `docker start moongate`.

## PostgreSQL persistence

The Compose file above uses the generated development credentials and one role for
everything. For a deployment, give the runtime a DML-only role, keep schema changes
on a separate role, and supply each `connection_string` as a `$NAME` environment
reference from your secret provider instead of a literal URI. Keep
`auto_sync_schema = false`. Run reviewed SQL with the migration runner, as in step
3, while the affected server is stopped. See
[PostgreSQL persistence](persistence.md) and the complete
[one login and two game instances example](docker-login-realms.md), which builds
local images, separates schema and runtime roles and runs schema jobs as Compose
profiles.

## Internal API port

The image declares `2593/tcp` and `2594/tcp`. `EXPOSE` does not start a listener or
publish a host port. Enable `[api]` and configure certificates and peer permissions
with [API host configuration](server-configuration.md#enable-the-internal-api-server).
For automatic certificate generation, follow [API certificates](api-certificates.md#docker):
keep the API disabled while creating identities and exchanging public PEM files,
and use a writable persistent volume. The read-only mount below is for
**externally provisioned** certificates.

Add to the `moongate` service of the Compose file:

```yaml
    environment:
      MOONGATE_API_CERTIFICATE_PASSWORD: "${MOONGATE_API_CERTIFICATE_PASSWORD:?Inject the PFX password into the shell environment}"
    volumes:
      - /absolute/path/to/api-certificates:/data/config/tls:ro
```

Obtain the password from your credential provider into the invoking shell's
environment. Do not write it into TOML or commit it in Compose or `.env` files. Keep
`certificate_path = "tls/server.pfx"` and `trusted_root_paths = ["tls/root.pem"]`
in `/data/config/moongate.toml`; these resolve under `/data/config`. The mounted
files must be readable by the image's non-root user.

Clients on the same Compose network can connect to `moongate:2594` without a
published API port. Provision the server certificate with the DNS name the client
uses as its TLS target host. A client on the Docker host can use a loopback-only
mapping added under `ports`:

```yaml
      - "127.0.0.1:2594:2594"
```

For another private-network host, publish on the Docker host's private IP instead.
Keep this internal channel on the private network. If you change `api.port`, use
that value as the mapping's container port and the client's destination port; the
Dockerfile's `EXPOSE` metadata remains the default 2594.

Check `docker compose logs moongate`: disabled APIs produce an activation warning;
enabled APIs log the bound endpoint and registered contract/handler counts. Bad
TLS configuration or an occupied listener port fails startup. The API speaks
MessagePack over mutual TLS/TCP, so an HTTP request or `curl` is not an API probe.
There are no built-in login or realm operations yet; register handlers first.

## Storage and multiple instances

A new named volume inherits the writable ownership prepared by the image. If you
replace it with a host bind mount, make that directory writable by the container
user; changing the mount does not change host ownership automatically.

`MOONGATE_ROOT` and `--root-directory` can select another server root. If you change
it, mount persistent storage at that path too. For multiple instances, use a
separate data volume and a different published host port for each server. Do not
share one root between running servers, and give each realm its own database.
The `mode` setting does not yet select separate login and game runtimes; see
[Implementation status](implementation-status.md).

## Update an instance

Read the target version's [changelog](../CHANGELOG.md), stop the server, and follow
your operator data-protection policy before upgrading. Change the pinned image tag
in `compose.yaml`:

```sh
docker compose stop
docker compose pull
docker compose up -d
docker compose logs --tail 100 -f moongate
```

The named volumes are reused. With plain `docker run`, stop and remove the old
container, then recreate it with the new image tag and the same named data volume
and client mount. Removing a container does not delete its named volume. If the new
release ships new core SQL, startup reports pending migrations: apply them as in
first-start step 3 before starting the server.

## Build the image from source

To run unreleased work, build the current checkout from the repository root:

```sh
docker build -f src/Moongate.Server/Dockerfile -t moongate:local .
```

Use `moongate:local` in place of the registry tag. The `sample-plugin` build stage
adds the sample plugin bundle; the [login and realms example](docker-login-realms.md)
uses it.

## UOX3 content conversion

Images after 0.6.0 include `/app/mg-uoxconv`, the same tool as
`src/Moongate.UoxItemConverter` (see [Migrate from UOX3](uox3-migration.md)),
published as a self-contained single file. Run it as a one-shot job, mounting a UOX3
`.dfn` source directory read-only and an output directory for the converted TOML:

```sh
docker run --rm --entrypoint /app/mg-uoxconv \
  --user "$(id -u):$(id -g)" \
  -v /path/to/uox3/data/dfndata/items:/uox-source:ro \
  -v /path/to/templates:/uox-out \
  ghcr.io/moongate-community/moongate:latest \
  --source /uox-source --destination /uox-out/items --loot-destination /uox-out/loots
```

`--user "$(id -u):$(id -g)"` matters: the image otherwise runs as its own non-root
user, which cannot write into a bind-mounted host directory it does not own. The
`/data` root avoids the same mismatch because the image creates that directory
pre-owned by the runtime user at build time.

## Troubleshooting

- **Exits on first start:** expected once, before the configuration is edited. After
  that, check the logs for the configuration or client-data error.
- **`Postgres connection` failure:** the database host must be reachable from the
  container. `localhost` is the container itself; use the Compose service name or a
  host address. Check that both databases exist and the role can log in.
- **`The core migrations directory is missing`:** set
  `persistence.migrations_directory = "/app/migrations"` as in step 2, or prepare the
  root with `mgboot`.
- **Pending migrations:** run the migration runner as in step 3 for the named target.
- **Cannot read client files:** confirm the host path exists and is mounted at
  `/uo`, with permission for the container user to read it.
- **Cannot write config or generated files:** check `/data` volume ownership,
  especially for bind mounts.
- **Cannot connect:** check `docker ps`, the `2593:2593` mapping, the listener
  configuration, and the host firewall.

See [Configuration](server-configuration.md) for all TOML settings and CLI limits,
[First start](getting-started.md#files-and-process-ownership) for PID ownership,
and [Diagnostics](diagnostics.md) for process metrics and diagnostic events.
