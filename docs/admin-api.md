# Administration API

Moongate embeds an optional gRPC plugin for private-network administration. It exposes account login, server information, paginated account listing, account creation and session revocation. A future React panel should call its own backend; that backend calls these gRPC endpoints. The browser does not connect directly to shard ports.

`MoongateAdminPlugin` is registered in `Program.cs` after `MoongateUltimaPlugin`. It ships with the server, never through the `plugins/` directory. Its default port is **2590**. It is **disabled by default**.

## Enable an endpoint

Add this section to `<MOONGATE_ROOT>/config/moongate.toml`:

```toml
[admin_api]
enabled = true
listen_address = "127.0.0.1"
port = 2590
session_lifetime_minutes = 30
max_receive_message_bytes = 65536
max_concurrent_calls = 64
allow_insecure_loopback = false
certificate_path = "certificates/admin.pfx"
certificate_password = "$MOONGATE_ADMIN_CERTIFICATE_PASSWORD"
```

Use the interface address reachable by the private panel backend. Both `listen_address = "*"` and `listen_address = "0.0.0.0"` bind all IPv4 interfaces; `"*"` is an alias for `"0.0.0.0"`, including inside a private container network. Use `"::"` to bind the IPv6 wildcard address. Wildcard binds require TLS. Restrict network access to trusted hosts. Restart after configuration changes.

The endpoint uses standard server TLS over HTTP/2. It does not use mTLS, peer certificates or the Redis game-handoff secret. Obtain a server PFX from your private CA with a private key, server-authentication usage and DNS names matching the endpoints clients use. Put it at the configured path, readable only by the Moongate service account. Distribute the **public CA certificate**, not the server private key, to backend clients. Clients must validate both the trust chain and hostname.

A relative certificate path resolves against `MOONGATE_ROOT`; environment variables and `~` are supported. A passwordless PFX uses `certificate_password = ""`. Otherwise supply the password through an environment reference populated from your secret store. Certificates are not generated automatically. Missing/unreadable/expired certificates, missing environment variables or an occupied port fail enabled startup with a redacted error. Disabled endpoints neither load certificates nor resolve password variables.

For local development only:

```toml
[admin_api]
enabled = true
listen_address = "127.0.0.1"
port = 2590
allow_insecure_loopback = true
```

This explicit override allows plaintext HTTP/2 only on literal loopback addresses (`127.0.0.1` or `::1`). Hostnames, wildcard addresses and Docker bridge interfaces cannot use the override. It logs a warning. The supplied sample clients require TLS; use an HTTP/2 client configured for plaintext when testing this override.

Settings accept ports 1–65535, session lifetimes 1–1440 minutes, receive limits 1024–1048576 bytes, and concurrent-call limits 1–1024. Defaults are shown above. The API accepts calls only after server startup subscribers finish; shutdown rejects new calls and drains existing work before dependencies stop.

## Provision the first administrator

Run these in the **Login or Standalone local console** (press `*` to unlock it):

```text
account create <username> <password> Administrator
account api-access <username> on
```

Replace placeholders with credentials from your secret store. Console arguments cannot contain spaces. `account api-access` rejects in-game invocation. Disable access with `account api-access <username> off`; this also revokes prior administrative sessions.

`CanAccessApi` defaults to false. Migration `auth/0004_account_admin_api_access.sql` adds the column with that default; if an existing development schema already has the column, explicit true values are preserved. Apply pending SQL with the [migration workflow](persistence-migrations.md). Ordinary game login is independent of API access.

## Roles and permissions

| RPC | Login / Standalone | Game | Permission |
| --- | --- | --- | --- |
| `AdminLogin.Login` | Yes | No | Valid password, unlocked account, `CanAccessApi = true`, defined account type |
| `AdminSession.Logout` | Yes | Yes | Well-formed presented token; already absent is successful |
| `AdminServer.GetServerInfo` | Yes | Yes | Any valid administrative session |
| `AdminAccounts.ListAccounts` | Yes | No | Administrator |
| `AdminAccounts.CreateAccount` | Yes | No | Administrator |
| `AdminAccountSessions.RevokeAccountSessions` | Yes | No | Administrator |

Game hosts never resolve Accounts services or connect to the Accounts database. Unavailable role-specific services return `UNIMPLEMENTED`. Configure endpoint addresses in the panel backend; administration endpoints are not advertised in realm discovery.

Regular and GameMaster accounts may read server information when API-enabled, but cannot list/create accounts or revoke others' sessions. Unknown account types are rejected. `CreateAccount` can assign any defined type and explicitly enable API access because only Administrators can invoke it.

## Call the API

The wire package is `moongate.admin.v1`. Obtain generated C# clients from `Moongate.Admin.Contracts`, or generate any supported language from the raw files under `proto/moongate/admin/v1/`. Each release includes `moongate-admin-protos-<version>.zip`. Standard `google/protobuf` imports come from the compiler distribution.

A request sequence:

1. Call `AdminLogin.Login` on a Login endpoint with `username` and `password`.
2. Keep the returned `access_token` in backend memory. Send `authorization: Bearer <token>` metadata on protected calls to either Login or Game.
3. Use `AdminAccounts.CreateAccount` and `ListAccounts` on Login, or `AdminServer.GetServerInfo` on either endpoint.
4. Call `AdminSession.Logout` on either endpoint to remove that token globally.

Runnable examples: [C# client](../samples/Moongate.Admin.Client/README.md), [Python client](../samples/admin-python/README.md). Both use standard TLS verification, environment-supplied credentials, bounded deadlines and no automatic mutation retries. They create a test account that remains in Accounts; use disposable environments for verification.

`ListAccounts` uses database keyset pagination: start with `after_account_id = 0`, send the returned `next_after_account_id` on the next call, stop when it is zero. Default page size is 50; maximum is 200. IDs are nonzero `uint32` for existing accounts. Summaries contain username, ID, role, access/lock flags and UTC creation time, never passwords, hashes or email.

Omitting `CreateAccount.account_type` means Regular. Explicit `UNSPECIFIED` and unknown enum values fail. `can_access_api` defaults to false. Usernames must contain 1–255 characters; passwords are nonblank and at most 1024 UTF-8 bytes. NUL characters are rejected. Username matching retains existing case-sensitive account semantics.

## Sessions, revocation and failures

Tokens have 256 random bits and an absolute lifetime of 30 minutes by default. There is no sliding renewal or refresh token: log in again after expiry. Redis stores only token digests and safe identity fields under `moongate:admin:`, separately from realm leases/handoff tickets. Up to 64 active sessions are allowed per account. Administrative sessions contain **no encryption layer of their own**; secure your private Redis deployment and credentials as described in [configuration](server-configuration.md).

Each protected call checks Redis. Logout and account-wide revocation apply across hosts immediately for newly admitted requests. An ephemeral Redis restart invalidates sessions; a server restart does not renew their expiry. A Redis outage fails closed with `UNAVAILABLE`. Login is limited across hosts to 10 attempts/minute per exact username and 30/minute per direct peer address; forwarded-address headers are not trusted.

Supported account security changes go through `IAccountAdminAccessService.SetApiAccessAsync`, `UpdateAccessAsync`, `ChangePasswordAsync`, or `RevokeSessionsAsync`. These coordinate PostgreSQL row locks with Redis authorization generations. A login racing a security change cannot retain a stale privileged session. A partially failed mutation leaves access blocked; a later valid login recovers against the authoritative committed account state. **Direct SQL and generic `IDataAccess` updates bypass immediate revocation.** Use the supported service methods for security changes.

| Status | Meaning |
| --- | --- |
| `INVALID_ARGUMENT` | Invalid input, enum, cursor ID or page size |
| `UNAUTHENTICATED` | Invalid credentials or missing, expired or revoked token |
| `PERMISSION_DENIED` | Valid session lacks the required role |
| `ALREADY_EXISTS` | Username already exists |
| `NOT_FOUND` | Revocation target does not exist |
| `RESOURCE_EXHAUSTED` | Login/session/concurrent-call limit reached, or request too large |
| `UNAVAILABLE` | Dependency outage or endpoint not ready/stopping |
| `UNIMPLEMENTED` | This server role does not expose the RPC |
| `CANCELLED` / `DEADLINE_EXCEEDED` | Caller cancellation or bounded execution ended |
| `INTERNAL` | Unexpected failure; inspect safe server logs |

Calls have a maximum server execution window of 15 seconds. Cancellation does not undo a committed account creation. If a create response is lost, a retry may return `ALREADY_EXISTS`; use listing to reconcile. There is no exactly-once guarantee or automatic retry policy. Revocation does not roll back writes from already admitted requests.

Audits record operation, account IDs, outcome and request correlation; no passwords, tokens or request bodies. Future live-world mutations must be scheduled through the GameLoop. This version exposes no character editing or arbitrary command execution.

## Docker and verification

The image documents port 2590, but `EXPOSE` does not enable or publish it. The [private Compose override](../examples/docker/login-realms/compose.admin.yaml) enables TLS with mounted operator-provided PFX files and publishes no administration port. See the [Compose guide](docker-login-realms.md).

Repository checks:

```sh
bash scripts/verify-admin-protos.sh
bash examples/docker/login-realms/admin-smoke.sh
```

The first requires the standard PostgreSQL/Redis test environment variables and uses a temporary Python environment plus real TLS fixtures. The second builds the server images, creates its own disposable Compose project and test CA, runs the Python client from the private Docker network, and removes only its own containers/volumes/certificates.
