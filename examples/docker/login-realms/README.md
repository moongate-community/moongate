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

## Optional administration API

Provide an absolute `MOONGATE_ADMIN_CERT_DIRECTORY` containing `login.pfx`, `game-1.pfx`, and `game-2.pfx`, readable by the container service user. Each certificate needs a private key, server authentication usage, and DNS SAN matching the host the backend uses (for example `login`, `game-1`, `game-2` inside the Compose network). Give clients the issuing public CA certificate. Set `MOONGATE_ADMIN_CERTIFICATE_PASSWORD` from your secret store if the PFX files are protected, otherwise leave it empty.

```sh
docker compose -f compose.yaml -f compose.admin.yaml up --build -d
```

Run the existing schema jobs first as described above. The override replaces only the runtime config sources and adds read-only certificate mounts. All three hosts listen on private port 2590 with TLS; **no administration port is published to the host**. Put the future panel backend on this private network. Do not enable plaintext mode on a container interface.

Provision the first Administrator through the Login local console with `account create` and `account api-access <username> on`. See [Administration API](../../../docs/admin-api.md) for permissions and clients.

`bash admin-smoke.sh` builds the images and exercises Login → Create/List → Game information → Game logout using a Python client and temporary CA. It owns a fresh Compose project, isolated databases and test certificates; cleanup removes only that project. It does not require real UO client files for this transport/account check and does not touch existing containers.
