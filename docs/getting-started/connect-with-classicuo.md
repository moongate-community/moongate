# Connect with ClassicUO

Moongate speaks to **ClassicUO 7.x** clients. This page assumes the server is
[up and listening](install-and-first-launch.md) and you have a ClassicUO
installation whose data files the server was started with.

## Client settings

In the ClassicUO launcher / `settings.json`:

| Setting | Value |
|---|---|
| Server IP | the machine running Moongate — `127.0.0.1` locally |
| Server port | `2593` (the default; see [Configuration](configuration.md)) |
| UO directory | the same client-files directory you passed as `--uo-directory` |

If the client is not on the same machine as the server, set
`Network.PublicAddress` in `moongate.yaml` to an address the client can
reach — it is what the server advertises in the server list and in the
game-server redirect, and it defaults to `127.0.0.1`.

## Logging in

Log in with the seeded account (**admin** / **admin**, until you change it).
The flow the server implements today:

1. **Account login** — credentials are verified against the persisted
   accounts; a wrong password or a deactivated account is rejected with the
   proper denial code.
2. **Server list** — one entry, named after `ShardName` from
   `moongate.yaml` (default `Moongate`), advertising `PublicAddress`.
3. **Select server** — the client is redirected to the game server (same
   process, same port) with a one-time handoff key that expires after 30
   seconds.
4. **Character list** — your characters, with the modern (post-AOS) client
   feature flags always enabled.
5. **Character creation** — creating a character builds and persists your
   player mobile.
6. **World entry** — creating or selecting a character loads you into the
   map, with your own mobile, its equipment and hair, your stats, and the
   season of the facet you stand on.

> [!IMPORTANT]
> **The world you enter is empty, and you cannot move yet.** World entry is
> self-only: the server sends you your own character, but nearby mobiles and
> items are not broadcast to you, and movement requests are not handled. You
> will load into the map and stand still, alone. This is the current frontier
> of the project.

## Troubleshooting

- **Server refuses to start with a UO-directory error** — the
  `--uo-directory` path (or `UltimaDirectory`) doesn't contain the client
  files the loaders need.
- **Client connects but the server list is empty or the redirect fails** —
  check `Network.PublicAddress`: it must be reachable *from the client*, not
  from the server.
- **Login rejected** — the only seeded account is `admin`/`admin`; there is
  no in-band account auto-creation.
