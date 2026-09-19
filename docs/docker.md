# Run with Docker

Each Moongate release publishes a **linux/amd64** image to
[GitHub Container Registry](https://github.com/moongate-community/moongate/pkgs/container/moongate).
Images are tagged with the version (without `v`) and with `latest`. The examples
below pin `0.4.0`; choose the version you want from the [changelog](../CHANGELOG.md)
or [GitHub releases](https://github.com/moongate-community/moongate/releases).
The site header displays the current version.

You need Docker and your own Ultima Online client files. The image does not
include those files. TCP port **2593** is the default game listener.

## First start

Pull the image and create persistent storage:

```sh
docker pull ghcr.io/moongate-community/moongate:0.4.0
docker volume create moongate-data
```

Replace `/absolute/path/to/ultima` with your client directory. Mount it read-only;
server configuration, logs, plugins, scripts, and saves belong in `/data`:

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

## Storage and multiple instances

The image sets `MOONGATE_ROOT=/data` and runs as the .NET image's non-root user.
A new named volume inherits the writable ownership prepared by the image.
If you replace it with a host bind mount, make that directory writable by the
container user; changing the mount does not change host ownership automatically.

`MOONGATE_ROOT` and `--root-directory` can select another server root. If you change
it, mount persistent storage at that path too. For multiple instances, use a
separate data volume and a different published host port for each server. Do not
share one root or save directory between running servers.

The `mode` setting currently defines the `login`, `game`, or `standalone`
configuration contract. It does not yet select separate login/game service
runtimes; see the [overview](../README.md#server-mode).

## Update an instance

Read the target version's [changelog](../CHANGELOG.md), stop the server, and back up
its data volume before upgrading. Change the pinned image tag in `compose.yaml`:

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
- **Cannot write config or saves:** check `/data` volume ownership, especially for
  bind mounts.
- **Cannot connect:** check `docker ps`, the `2593:2593` mapping, the listener
  configuration, and the host firewall.

See [Diagnostics](diagnostics.md) for process metrics and diagnostic events.
