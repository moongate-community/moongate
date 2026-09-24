# Login and realms Compose example

This example builds one login server, two independent game servers, PostgreSQL with separate Accounts/Realm databases, and one private Redis instance. Redis holds expiring realm leases and one-use login handoff tickets. The UO client ports are 2593 for login and 2595/2596 for the two games; Redis is not published to the host.

Copy `.env.example` to `.env`, set `UO_DATA_PATH`, and export the nine named secrets from Bitwarden into the invoking shell. The Redis password and handoff secret must be distinct hexadecimal values. Then run:

```sh
docker compose config --quiet
docker compose build login game-1 game-2 auth-schema-apply schema-preview schema-apply migration-status
docker compose up -d --wait postgres redis
docker compose --profile schema run --rm auth-schema-apply
docker compose --profile schema run --rm schema-apply
docker compose up -d login game-1 game-2
```

See the [full setup and operations guide](../../../docs/docker-login-realms.md) for secret names, PostgreSQL privileges, advertised client addresses, realm leases, shutdown, and the disposable `smoke.sh` test.
