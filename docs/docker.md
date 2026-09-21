# Run with Docker

Each Moongate release publishes a **linux/amd64** image to
[GitHub Container Registry](https://github.com/moongate-community/moongate/pkgs/container/moongate).
Images are tagged with the version (without `v`) and with `latest`. The examples
below pin `0.4.0`; choose the version you want from the [changelog](../CHANGELOG.md)
or [GitHub releases](https://github.com/moongate-community/moongate/releases).
The site header displays the current version.

You need Docker and your own Ultima Online client files. The image does not
include those files. TCP port **2593** is the default game listener. Current source
builds also declare **2594/tcp** for the optional internal API; it is disabled by default.

## First start

Pull the image and create persistent storage:

```sh
docker pull ghcr.io/moongate-community/moongate:0.4.0
docker volume create moongate-data
```

Replace `/absolute/path/to/ultima` with your client directory. Mount it read-only;
server configuration, logs, plugins, and scripts belong in `/data`:

```sh
docker run -d --name moongate \
  --mount type=volume,source=moongate-data,target=/data \
  --mount type=bind,source=/absolute/path/to/ultima,target=/uo,readonly \
  -p 2593:2593 \
  ghcr.io/moongate-community/moongate:0.4.0
```

On a fresh volume the server writes `/data/config/moongate.toml` and exits because
`ultima_path` still contains its placeholder. Copy out the generated configuration:

```sh
docker cp moongate:/data/config/moongate.toml ./moongate.toml
```

Edit the existing `[ultima]` section in that file:

```toml
[ultima]
ultima_path = "/uo"
```

The path is **inside the container**, so it must match the `/uo` mount, not the
host path. Keep the other generated settings. Copy the file back and start again:

```sh
docker cp ./moongate.toml moongate:/data/config/moongate.toml
docker start moongate
docker logs --tail 100 -f moongate
```

Stop the log viewer with Ctrl+C; it does not stop the detached container.
Use `docker stop moongate` for a normal shutdown and `docker start moongate`
to resume with the same volume.

## Docker Compose

For a local source build with three separate processes, use the
[one login and two game instances example](docker-login-realms.md). It includes
one PostgreSQL service with three databases, runtime/schema role separation,
schema jobs, independent server storage and ports, and the current limitations of
the login/game modes.

Save this as `compose.yaml`, replacing the client directory:

```yaml
services:
  moongate:
    image: ghcr.io/moongate-community/moongate:0.4.0
    ports:
      - "2593:2593"
    volumes:
      - moongate-data:/data
      - /absolute/path/to/ultima:/uo:ro

volumes:
  moongate-data:
```

Start it with `docker compose up -d`. A new Compose volume also requires the
first-start configuration above. Use the Compose service name for the copy steps:

```sh
docker compose cp moongate:/data/config/moongate.toml ./moongate.toml
# Edit [ultima].ultima_path to /uo in moongate.toml.
docker compose cp ./moongate.toml moongate:/data/config/moongate.toml
docker compose start moongate
docker compose logs --tail 100 -f moongate
```

Compose creates a project-scoped named volume. Keep the same Compose project name
and directory when restarting the same world. `docker compose down` removes the
containers but retains that volume; adding `--volumes` deletes the persisted data.

## PostgreSQL persistence

The ordinary image does not bundle PostgreSQL or the sample plugin. A server with
registered persistence entities uses a `postgres://user:password@host/database`
URI in its `connection_string` setting. Use a `$NAME` environment reference to
supply the URI from a secret provider. Keep `auto_sync_schema = false` and
give the runtime process a DML-only role. Run reviewed schema preview/apply jobs
with the same plugin bundle and a separate schema connection while the relevant
runtime is stopped. See [PostgreSQL persistence](persistence.md) and the complete
[login and realms example](docker-login-realms.md).

## Internal API port

API hosting is available in builds containing this change; the pinned `0.4.0`
image examples above predate it. Build the current checkout from the repository
root to try it before the next release:

```sh
docker build -f src/Moongate.Server/Dockerfile -t moongate:local .
```

The image declares `2593/tcp` and `2594/tcp`. `EXPOSE` does not start a listener or
publish a host port. Enable `[api]` and configure certificates/peer permissions
using [API host configuration](server-configuration.md#enable-the-internal-api-server).
For automatic certificate generation, follow [API certificates](api-certificates.md#docker):
keep the API disabled while creating identities and exchanging public PEM files,
and use a writable persistent volume. The read-only mount below is for
**externally provisioned** certificates.

A private Compose deployment can use:

```yaml
services:
  moongate:
    image: moongate:local
    ports:
      - "2593:2593"
    environment:
      MOONGATE_API_CERTIFICATE_PASSWORD: "${MOONGATE_API_CERTIFICATE_PASSWORD:?Inject the PFX password into the shell environment}"
    volumes:
      - moongate-data:/data
      - /absolute/path/to/ultima:/uo:ro
      - /absolute/path/to/api-certificates:/data/config/tls:ro

volumes:
  moongate-data:
```

Obtain the password from your credential provider into the invoking shell's
environment. Do not write it into TOML or commit it in Compose/`.env` files. Keep
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
There are no built-in login/realm operations yet; register handlers first.

## Storage and multiple instances

The image sets `MOONGATE_ROOT=/data` and runs as the .NET image's non-root user.
A new named volume inherits the writable ownership prepared by the image.
If you replace it with a host bind mount, make that directory writable by the
container user; changing the mount does not change host ownership automatically.

`MOONGATE_ROOT` and `--root-directory` can select another server root. If you change
it, mount persistent storage at that path too. For multiple instances, use a
separate data volume and a different published host port for each server. Do not
share one root between running servers, and give each realm its own database.

The `mode` setting currently defines the `login`, `game`, or `standalone`
configuration contract. It does not yet select separate login/game service
runtimes; see the [overview](../README.md#server-mode).

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

The named volume is reused. With plain `docker run`, stop and remove the old
container, then recreate it with the new image tag and the same named data volume
and client mount. Removing a container does not delete its named volume.

## Troubleshooting

- **Exits on first start:** edit the generated `ultima_path` and restart; check the
  logs for any further configuration or client-data error.
- **Cannot read client files:** confirm the host path exists and is mounted at
  `/uo`, with permission for the container user to read it.
- **Cannot write config or generated files:** check `/data` volume ownership, especially for
  bind mounts.
- **Persistence connection/schema failure:** verify the configured environment
  variable, PostgreSQL URI encoding, plugin bundle, role grants, and schema
  preview output. Do not give the normal runtime a DDL credential.
- **Cannot connect:** check `docker ps`, the `2593:2593` mapping, the listener
  configuration, and the host firewall.

See [Configuration](server-configuration.md) for all TOML settings and CLI limits,
[First start](getting-started.md#files-and-process-ownership) for PID ownership,
and [Diagnostics](diagnostics.md) for process metrics and diagnostic events.

## Database migration job

Images built from this source include the isolated runner at
`/app/migration-runner/Moongate.MigrationRunner` and core SQL at `/app/migrations`.
Run it as a one-shot job with the same SQL/plugin bundle as the server, a selected
`--target auth|world`, and a root containing a TOML with schema-role credentials.
Normal server startup validates migrations but does not apply them by default.
See the [Compose maintenance example](docker-login-realms.md#review-and-apply-schema-changes)
and [versioned SQL workflow](persistence.md#generate-review-and-apply).
