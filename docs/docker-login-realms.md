# Docker example: one login and two game instances

The [Compose example](../examples/docker/login-realms/compose.yaml) builds three
Moongate images from the local checkout and starts separate processes with
`mode = "login"`, `mode = "game"`, and `mode = "game"`. Each process has its own
configuration, published client port, and persistent data volume.

**This is a deployment topology example, not a working login-to-realm flow.**
The current `mode` setting does not select startup services: all three instances
still start the same runtime, including the game loop and persistence. Shared
accounts, realm registration/discovery, login handoff, and role-specific startup
are not implemented. Starting this example does not provide a playable shard or
a realm-selection screen. See [Transport and game ownership](network-game-separation.md).

## Topology

| Service | Configured mode | Host client endpoint | Container client endpoint | Persistent volume |
| --- | --- | --- | --- | --- |
| `login` | `login` | `127.0.0.1:2593` | `login:2593` | `login-data` |
| `game-1` | `game` | `127.0.0.1:2595` | `game-1:2593` | `game-1-data` |
| `game-2` | `game` | `127.0.0.1:2596` | `game-2:2593` | `game-2-data` |

All containers join the project's `moongate` bridge network. Docker resolves the
service names there. This provides connectivity, not application-level discovery.
There is no startup dependency between services. You can stop any one of them
independently.

The internal API is disabled in every TOML file, and Compose publishes no API
ports. Its configured container port remains `2594` for later use.

## Build and start

Use a current Docker Engine or Docker Desktop with Docker Compose v2 or later.
You also need your own readable Ultima Online client directory. Client files are
mounted read-only at `/uo`; they are not included in the images. Keep them outside
the repository so they are outside the Docker build context.

1. From the repository root, enter the example directory and create your local
   environment file:

   ```sh
   cd examples/docker/login-realms
   cp .env.example .env
   ```

2. Edit `.env`. Set `UO_DATA_PATH` to the **absolute host path** of your client
   directory. Quote the value if it contains spaces. This file is ignored by Git;
   it contains paths and port settings, not credentials. The existing TOML files
   already use `/uo`, so no initial configuration-generation step is needed.

3. Validate the configuration and build/start all three instances:

   ```sh
   docker compose config --quiet
   docker compose up --build -d
   docker compose ps
   docker compose logs --tail 100
   ```

Compose builds the repository's `src/Moongate.Server/Dockerfile` for each service.
The images use the same executable and share cached build layers; their generated
names are scoped to the Compose project and service. No pre-published Moongate
image is required. Building still needs access to the .NET base images and NuGet.
See the [Docker Compose build reference](https://docs.docker.com/reference/compose-file/build/).

The default project name is `moongate-login-realms`. To run another copy, choose
another project name with `docker compose -p another-example ...` and three unused
host ports. Use that same project name for every subsequent command so Compose
finds the same containers and volumes.

## Configuration and storage

The three [TOML configurations](../examples/docker/login-realms/config) are mounted
read-only at `/data/config/moongate.toml`. Edit the source files on the host and
recreate the affected service to apply them:

```sh
docker compose up -d --no-build --force-recreate game-1
```

Settings omitted from these minimal files retain the server's model defaults.
See [Configuration](server-configuration.md) for world saves, diagnostics,
scripting and the internal API. A missing `scripts/init.lua` produces a warning;
the server can still start without a user script.

Each named volume mounts at `/data`, the image's `MOONGATE_ROOT`. It contains that
process's PID ownership files, logs, plugins, scripts and saves. New named volumes
inherit the image's writable ownership and the server runs as a non-root user.
The mounted TOML remains host-managed and is not part of a volume backup; back up
those files separately. Files generated under other configuration paths belong
to the instance's volume.

**Never share the three data volumes or their save directories.** Persistence is
local to each process; this example does not synchronize accounts or worlds. The
only shared input is the read-only client directory. Compose adds the project
prefix to volume names, such as `moongate-login-realms_game-1-data`.

`docker compose down` removes the containers and network but keeps named volumes.
Start again with `docker compose up -d --no-build` to reuse them. Adding
`--volumes` to `down` deletes the persisted data; avoid it when keeping a world.

## Ports and independent lifecycle

The `.env` file exposes these optional overrides:

| Variable | Default | Purpose |
| --- | --- | --- |
| `MOONGATE_BIND_ADDRESS` | `127.0.0.1` | Host address for all three client ports |
| `LOGIN_PORT` | `2593` | Login-designated instance's host port |
| `GAME_1_PORT` | `2595` | First game instance's host port |
| `GAME_2_PORT` | `2596` | Second game instance's host port |

For clients on another machine, set the bind address to the Docker host's LAN
address and permit the chosen TCP ports in its firewall. Clients use that host
address and the published ports; Compose names such as `game-1` are for containers
on the Docker network. All three TOML listeners remain `0.0.0.0:2593` **inside**
their containers. Recreate containers with `docker compose up -d --no-build`
after changing mappings.

Stop and resume just the login-designated process:

```sh
docker compose stop login
docker compose ps
docker compose start login
```

The same commands accept `game-1` or `game-2`. To stop the whole example while
retaining containers and storage, run `docker compose stop`. The example allows
60 seconds for graceful shutdown; increase `stop_grace_period` if your saves need
longer. It does not automatically restart failed processes, so startup errors
remain visible in `docker compose ps -a` and the logs.

## Internal APIs and certificates

The default API-disabled warning is expected. No certificates are required to
start this example, and there are no built-in account/realm operations or outbound
peer connections. Enabling a listener alone does not connect the three processes.

For a plugin or application that registers API contracts, follow the
[API certificate guide](api-certificates.md). Give each instance its own identity
in its own persistent volume. Include its service name (`login`, `game-1`, or
`game-2`) in `certificate_dns_names` so it matches the client's TLS target host.
Use a writable path under `/data/config/tls`, not the read-only TOML mount.

Generate certificates with the API disabled, exchange only public PEM files, and
configure mutual trust and explicit peer permissions before enabling listeners.
Never share private PFX files between instances or commit them to the example.
`allowed_operations = ["*"]` grants every registered nonzero operation to that
trusted peer; empty or omitted permissions deny all incoming operations.

Clients on the same bridge network can then use `login:2594`, `game-1:2594`, or
`game-2:2594`. Keep these ports unpublished on the host. The API is MessagePack
over mutual TLS/TCP, not HTTP, and requires your registered operations and client
integration.

## Troubleshooting

- **Missing `UO_DATA_PATH`:** create/edit `.env` or export the variable before
  invoking Compose. No default client path is silently created.
- **Mount or permission error:** check that the absolute client directory exists
  and is readable by the container user. The bind mount has automatic host-path
  creation disabled. TOML files must also be readable by that user.
- **Port already allocated:** select three different unused host ports in `.env`
  and run `docker compose up -d --no-build` again.
- **Container exits:** use `docker compose ps -a` and
  `docker compose logs --tail 100 game-1` (or the affected service). Correct the
  reported configuration/data error and recreate that service.
- **Login does not list the game instances:** this flow is not implemented yet;
  service DNS and `mode` settings do not provide realm discovery or handoff.
