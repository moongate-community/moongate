# Login & shard select

Seed, account auth, server list, shard select, game-server handoff.

| Opcode | Name | Dir | Size | Description |
|---|---|---|---|---|
| [`0x80`](../incoming/0x80-account-login-request.md) | Account Login Request | C → S | — | Credentials for the login server. |
| [`0x82`](../outgoing/0x82-login-denied.md) | Login Denied | S → C | — | Rejects the login with a protocol reason code. |
| [`0x8C`](../outgoing/0x8c-connect-to-game-server.md) | Connect To Game Server | S → C | — | Redirects the client to the game port with an auth key. |
| [`0x91`](../incoming/0x91-game-server-login.md) | Game Server Login | C → S | — | The auth key from the redirect plus the account credentials. |
| [`0xA0`](../incoming/0xa0-select-server.md) | Select Server | C → S | — | The shard index the client picked from the server list. |
| [`0xA8`](../outgoing/0xa8-server-list.md) | Server List | S → C | — | Advertises the available shards. |
| [`0xBD`](../incoming/0xbd-client-version.md) | Client Version | C → S | Variable | The client answers the server's version request with its build string (e.g. |
| [`0xEF`](../incoming/0xef-login-seed.md) | Login Seed | C → S | — | Connection seed and client version, sent first by ClassicUO. |
