# Login & shard select

Seed, account auth, server list, shard select, game-server handoff.

| Opcode | Name | Dir | Size | Description |
|---|---|---|---|---|
| [`0x80`](../incoming/0x80-account-login-request.md) | Account Login Request | C → S | 62 bytes (fixed) | Credentials for the login server. |
| [`0x82`](../outgoing/0x82-login-denied.md) | Login Denied | S → C | 2 bytes (fixed) | Rejects the login with a protocol reason code. |
| [`0x8C`](../outgoing/0x8c-connect-to-game-server.md) | Connect To Game Server | S → C | 11 bytes (fixed) | Redirects the client to the game port with an auth key. |
| [`0x91`](../incoming/0x91-game-server-login.md) | Game Server Login | C → S | 65 bytes (fixed) | The auth key from the redirect plus the account credentials. |
| [`0xA0`](../incoming/0xa0-select-server.md) | Select Server | C → S | 3 bytes (fixed) | The shard index the client picked from the server list. |
| [`0xA8`](../outgoing/0xa8-server-list.md) | Server List | S → C | Variable | Advertises the available shards. |
| [`0xBD`](../incoming/0xbd-client-version.md) | Client Version | C → S | Variable | The client answers the server's version request with its build string (e.g. |
| [`0xEF`](../incoming/0xef-login-seed.md) | Login Seed | C → S | 21 bytes (fixed) | Connection seed and client version, sent first by ClassicUO. |
