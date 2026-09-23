# Login and realms Compose example

This example builds one login-designated process, two game-designated processes,
and one PostgreSQL 16 service with separate Accounts, Realm 1, and Realm 2
databases. It also includes one-shot schema preview and versioned SQL status/apply jobs and a disposable
end-to-end smoke test.

The login process checks Accounts and lists the two registered realms after a
successful account login. Each game process checks only its own Realm database
and maintains an mTLS lease with login. Realm selection, redirect and ticket
handoff are not implemented yet. Keep passwords out of `.env`: export the
required variables from Bitwarden or another process secret provider, provision
the three certificates described in the guide, then run `docker compose config --quiet`.

See the [full setup and operations guide](../../../docs/docker-login-realms.md)
for credential names, schema maintenance, privilege boundaries, world saves,
ports, volumes, and `smoke.sh`.
