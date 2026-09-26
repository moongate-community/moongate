# Docker example: one login and two game instances

The [Compose example](../examples/docker/login-realms/compose.yaml) builds the current source and runs one login process, two game processes, PostgreSQL 16 and private Redis 7.4. PostgreSQL holds separate Accounts, Realm 1 and Realm 2 databases. Redis holds short-lived realm leases and one-use login handoff tickets. Nothing in the example publishes Redis to the host.

A successful `0x80` login receives a filtered `0xA8` list. `0xA0` selects a realm; login sends `0x8C` with its IPv4 address, port and one-use key, then closes that login connection. The client reconnects to the chosen game port, sends the raw four-byte key as its seed, then `0x91` with the same key and credentials. The game consumes the ticket from Redis, associates the account with its local session and replies with the supported features (`0xB9`) and an empty character list (`0xA9`) with the starting cities. Character creation, selection and world entry are still under development.

## Topology

| Service | Database | Host client endpoint | Redis access |
| --- | --- | --- | --- |
| `login` | Accounts runtime only | `127.0.0.1:2593` | Leases and handoff tickets |
| `game-1` | Realm 1 runtime only | `127.0.0.1:2595` | Its lease and handoff tickets |
| `game-2` | Realm 2 runtime only | `127.0.0.1:2596` | Its lease and handoff tickets |
| `redis` | None | No published port | Private `moongate` bridge only |
| Schema profile jobs | Only their selected schema target | None | None |

Each server has its own `/data` volume. PostgreSQL has a separate persistent volume. Redis has no persistent volume: tickets and leases are deliberately ephemeral. A Redis restart invalidates both; running game processes republish their leases. A Redis outage prevents new realm lists and handoffs, while already admitted game sessions continue. Redis uses `maxmemory-policy noeviction`, so memory pressure rejects writes instead of silently removing live tickets.

`game-1` uses the Dockerfile's `sample-plugin` stage. Its plugin registers `sample_greeter.notes` in Realm 1; the schema jobs exercise the same plugin and reviewed SQL. `game-2` uses the ordinary image. Login has no Realm database credential, and games have no Accounts credential. All three share one Redis credential and a separate handoff secret; these servers are therefore one private trust domain. No peer certificate or per-realm ACL is needed.

Inside the containers, login listens on port 2593 and each game listens on 2595. Compose maps game 2 to host port 2596, which its `realm_directory.advertised_port` also declares. The default advertised IPv4 address is `127.0.0.1` for a client on the Docker host. For remote clients, set `advertised_address` in both game TOMLs to a host-reachable IPv4 address and publish the client ports on that host address. Set the same address in `game-1-admin.toml` and `game-2-admin.toml` if you use the administration override. The `0xA8` list has IPv4 addresses but no ports; `0x8C` supplies the selected realm's port.

## Prepare configuration and secrets

Use Docker Engine or Docker Desktop with Compose v2 and your own readable Ultima Online client directory. From the repository root:

```sh
cd examples/docker/login-realms
cp .env.example .env
```

Set `UO_DATA_PATH` in `.env` to an absolute host directory. Change host-facing ports or non-secret PostgreSQL names there if required. If you change `GAME_1_PORT` or `GAME_2_PORT`, set the matching `realm_directory.advertised_port` in `config/game-1.toml` or `config/game-2.toml` to that host port; update the corresponding `*-admin.toml` as well if you use the administration override. Otherwise the game redirect sends clients to the old port. Keep passwords and handoff keys out of `.env`, TOML and the repository.

Create nine distinct secret records in Bitwarden: seven PostgreSQL passwords, a 64-character hexadecimal Redis password and an independent hexadecimal handoff secret of at least 64 characters. Export them in the shell that runs Compose, substituting your actual Bitwarden item names:

```sh
export MOONGATE_POSTGRES_ADMIN_PASSWORD="$(bw get password moongate-postgres-admin)"
export MOONGATE_ACCOUNTS_SCHEMA_PASSWORD="$(bw get password moongate-accounts-schema)"
export MOONGATE_ACCOUNTS_RUNTIME_PASSWORD="$(bw get password moongate-accounts-runtime)"
export MOONGATE_REALM_1_SCHEMA_PASSWORD="$(bw get password moongate-realm-1-schema)"
export MOONGATE_REALM_1_RUNTIME_PASSWORD="$(bw get password moongate-realm-1-runtime)"
export MOONGATE_REALM_2_SCHEMA_PASSWORD="$(bw get password moongate-realm-2-schema)"
export MOONGATE_REALM_2_RUNTIME_PASSWORD="$(bw get password moongate-realm-2-runtime)"
export MOONGATE_REDIS_PASSWORD="$(bw get password moongate-redis)"
export MOONGATE_HANDOFF_SECRET="$(bw get password moongate-handoff)"
```

Compose mounts these values as secrets. The server entrypoint constructs `MOONGATE_REDIS_CONNECTION_STRING=redis:6379,password=...` and exports `MOONGATE_HANDOFF_SECRET` only in the runtime process environment. The mounted TOMLs contain references to those variable names, not their values. Redis reads its password from its own secret mount into a private in-memory config file. The PostgreSQL connection URIs are likewise assembled in memory from role-specific secrets. Before starting the server, the entrypoint runs `mgboot` on the volume, which adds any [shard data file](data-files.md) the image ships and the volume lacks, keeping the ones already there.

Validate without printing the rendered Compose model, then build:

```sh
docker compose config --quiet
docker compose build login game-1 game-2 auth-schema-apply schema-preview schema-apply migration-status
```

Missing secret inputs fail validation by name. Use `--quiet`: rendering the expanded model can expose values from Compose's early-validation extension.

## Provision databases and review SQL

The PostgreSQL init script creates six roles and three databases on the first empty volume. Schema roles own DDL; runtime roles have DML and required sequence access only. Initialization does not rerun when a container is recreated. Never run `docker compose down --volumes` on data you intend to keep.

Apply the built-in Accounts SQL before login starts:

```sh
docker compose up -d --wait postgres redis
docker compose --profile schema run --rm auth-schema-apply
```

Review and apply the sample plugin's World SQL for Realm 1:

```sh
docker compose --profile schema run --rm migration-status
docker compose --profile schema run --rm schema-preview
docker compose --profile schema run --rm schema-apply
docker compose --profile schema run --rm migration-status
```

The schema jobs do not start a Redis client. They use their selected schema-role PostgreSQL secret and run only the requested target. The runtime TOMLs set `auto_sync_schema = false` and use core SQL bundled at `/app/migrations`. For more on authoring and applying plugin SQL, see [Generate, review and apply](persistence-migrations.md#generate-review-and-apply).

## Start, observe and stop

```sh
docker compose up -d login game-1 game-2
docker compose ps
docker compose logs --tail 100 login game-1 game-2
```

The three runtime services wait for PostgreSQL and Redis health. Startup checks the role's database and Redis connection. Game processes publish `moongate:realm:1` and `moongate:realm:2`, each with a 15-second lease renewed every five seconds. To inspect them without displaying the Redis password:

```sh
docker compose exec -T redis sh -ec 'export REDISCLI_AUTH="$(cat /run/secrets/redis-password)"; redis-cli --raw HGETALL moongate:realm:1; redis-cli TTL moongate:realm:1'
```

The keys should have positive TTLs. Do not use `FLUSHDB`: Redis may hold other short-lived Moongate state. The example's [smoke script](../examples/docker/login-realms/smoke.sh) checks both leases beyond one full lease interval.

To create a test account, attach to the login console with `docker compose attach login`, press `*` to unlock commands, then run `account create <username> <password> [level]`. Save the password in Bitwarden. Detach with Docker's `Ctrl-P`, `Ctrl-Q` sequence so login keeps running. Game containers do not register this command.

Each `/data` volume contains that process's PID, logs, scripts and plugins; do not share one root between servers. Stop or recreate one game independently:

```sh
docker compose stop game-1
docker compose up -d --no-build --force-recreate game-1
```

`docker compose down` removes containers and the private bridge but retains named volumes. World saves are not PostgreSQL backups; set your database backup policy separately.

## Disposable smoke test

From the repository root:

```sh
sh examples/docker/login-realms/smoke.sh
```

The script creates a unique Compose project with temporary volumes and synthetic process-only credentials. It builds local images, checks PostgreSQL roles and schema behavior, starts all three hosts, verifies that both Redis leases have positive TTLs after a full lease interval, and checks clean shutdown. The UO `0x80`→`0x91` handoff and ticket replay are covered by the Redis-backed integration tests in the .NET suite.

## Troubleshooting

- **Compose says a variable is missing:** export the named value from Bitwarden in the same shell, or set `UO_DATA_PATH` in `.env`.
- **Redis health fails:** inspect `docker compose logs redis`, the secret mount and the container's private-network address. Redis has no host port to probe.
- **Login has no realms:** inspect `docker compose logs game-1 game-2` and the `moongate:realm:*` keys and TTLs. Verify that all three runtime containers share the same Redis service and handoff secret.
- **Startup reports pending SQL:** run the matching schema profile job while the affected runtime is stopped.
- **Client cannot enter the selected game:** confirm `advertised_address` is reachable from the client, the host port matches `advertised_port`, and the redirect key has not expired or been used.
- **Container exits:** inspect `docker compose ps -a` and its logs. Correct the database, Redis or configuration error before recreating it.

## Private administration endpoint

Port 2590 is reserved for optional gRPC administration and remains disabled in the default image/Compose configuration. Use server TLS on the private network; no mTLS or public port mapping is required. See the [administration guide](admin-api.md) and [opt-in Compose configuration](../examples/docker/login-realms/README.md#optional-administration-api).
