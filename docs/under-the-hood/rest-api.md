# REST API

Moongate exposes a versioned REST API for administration and for accounts to
ask about themselves. It is served by `Moongate.Http.Plugin`, one of the
built-in plugins, and it is genuinely optional: the game server hosts the web
application rather than the other way round, so removing the plugin removes the
whole web stack.

## Turning it off

Start the server with `--disable-web-plugin`:

```bash
dotnet run --project src/Moongate.Server -- --disable-web-plugin
```

The server then runs with no Kestrel, no listener and no `REST API listening`
log — it logs `HTTP is disabled` and nothing else changes, because no other
code path asks for it. Two plugins are skipped: `MoongateHttpPlugin`, which
owns the API's plumbing, and `MoongateApiEndpointsPlugin`, which registers the
endpoint groups the server would map.

## Configuration

The `http:` section of `moongate.yaml`:

```yaml
http:
  Address: 0.0.0.0
  Port: 8933
  Jwt:
    SigningKey: ''
    LifetimeMinutes: 60
    Issuer: moongate
```

`SigningKey` signs the tokens that carry account level, so it is the one value
worth thinking about:

- **Left empty**, the server mints a key on first start and writes it back to
  `moongate.yaml`. That is why it is minted rather than shipped as a constant:
  a key baked into the source would let anyone who read it mint
  `Administrator` tokens against every server that kept the default. Minting it
  once and persisting it also means tokens survive a restart — a key
  regenerated on every boot would silently invalidate every token issued
  before it.
- **Set but shorter than 32 bytes**, the server refuses to start and says so.
  HS256 cannot sign with it, and the tokens would be unverifiable. Not
  configuring a key is an omission worth covering; configuring a bad one is a
  mistake worth reporting.

## The same container

The web application runs on the game's own DryIoc container, so an endpoint
resolves the very singletons the game loop holds — there is no second, parallel
world behind the API.

This is also why the OpenAPI document comes from Swashbuckle rather than
`Microsoft.AspNetCore.OpenApi`, which would otherwise be the obvious choice on
.NET 10: `AddOpenApi` resolves its document services by key, and the DryIoc
adapter does not implement `IKeyedServiceProvider`. Scalar reads the document
either way.

## Authentication

`POST /api/v1/auth/login` trades account credentials for a bearer token:

```bash
curl -X POST http://127.0.0.1:8933/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"tom","password":"secret"}'
```

```json
{ "token": "eyJhbGciOiJIUzI1NiIs...", "expiresAt": "2026-07-16T17:35:12+00:00" }
```

Send it back as `Authorization: Bearer <token>`. The token carries the account
id, the username and the account level; it expires after
`Jwt.LifetimeMinutes`.

Login answers **one flat 401 whatever the reason** — wrong password, unknown
username, blocked account. The game client is told which of these it is,
because a player needs to know, but over HTTP that same detail tells an
attacker which usernames exist. The real reason goes to the log instead.

## Policies

Two policies divide the surface, both driven by the account level in the token:

| Policy   | Who                            | Applies to      |
| -------- | ------------------------------ | --------------- |
| `admin`  | `Administrator`, `GrandMaster` | `/api/v1/admin` |
| `player` | any authenticated account      | `/api/v1/player`|

A valid player token against an admin route gets a **403**, not a 401: it is a
good token that is simply not staff.

## Endpoints

| Method   | Route                                | Auth     | Returns                            |
| -------- | ------------------------------------ | -------- | ---------------------------------- |
| `GET`    | `/api/v1/version`                    | none     | shard name and build               |
| `POST`   | `/api/v1/auth/login`                 | none     | a bearer token and its expiry      |
| `GET`    | `/api/v1/admin/status`               | `admin`  | shard name, build, online sessions |
| `GET`    | `/api/v1/player/me`                  | `player` | the caller's username and level    |
| `GET`    | `/api/v1/admin/accounts`             | `admin`  | every account                      |
| `GET`    | `/api/v1/admin/accounts/{username}`  | `admin`  | one account, or 404                |
| `POST`   | `/api/v1/admin/accounts`             | `admin`  | 201 and a `Location`, or 400 / 409 |
| `PATCH`  | `/api/v1/admin/accounts/{username}`  | `admin`  | the updated account, or 400 / 404  |
| `DELETE` | `/api/v1/admin/accounts/{username}`  | `admin`  | 204, or 404 / 409 / 503            |

`/api/v1/version` is deliberately unauthenticated: a launcher or the website
checks compatibility with it, and it reveals nothing an operator would want
hidden.

`/api/v1/player/me` reports only what the token already carries. It does not
list the account's characters on purpose: that would read the mobile store,
which is single-writer on the game loop, and drag loop affinity into what is
otherwise a probe.

JSON is camelCase on the wire while the DTOs stay PascalCase in C#.

## Managing accounts

The account routes are the REST twin of Lua's `account.*` module, and reach the
same `IAccountService`.

```bash
curl -X POST http://127.0.0.1:8933/api/v1/admin/accounts \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"username":"alice","password":"secret","email":"a@b.c","level":"Player"}'
```

An omitted `level` means `Player`: an account that gains staff rights by
accident is the wrong way to fail. A level that is not an `AccountLevelType`
name is a 400, checked before anything is written, so a bad request cannot
leave a half-made account behind.

`PATCH` changes only the fields it carries — `level`, `isActive` and
`password` are each optional, and an absent one is left alone:

```bash
curl -X PATCH http://127.0.0.1:8933/api/v1/admin/accounts/alice \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"isActive":false}'
```

**Email cannot be changed after creation**: the account service has no setter
for it.

**The password is write-only.** It goes in through `POST` and `PATCH` and
appears in no response: the account resource reports `username`, `email`,
`level`, `isActive` and `characterCount`, and nothing else. `AccountEntity`
holds a password hash and an activation token, and neither ever leaves the
server.

**These routes say why they failed**, which looks like the opposite of the
login rule above and is not. Login answers a flat 401 because anyone can call
it, and the difference between "no such user" and "wrong password" tells an
attacker which usernames exist. Here the caller already holds a staff token, so
naming the reason is operational information for someone entitled to it.

### Deleting

`DELETE` removes the account **and every character on it**, with everything
those characters carry. It is the only route that touches world state, so it
does its work on the game loop and the request waits for the answer: deleting
checks whether a character is being played and then deletes, login runs on the
loop, and doing that check anywhere else lets a player log in between the two
and lose their character from under them.

- **409** — a character on the account is being played. Nothing is deleted.
- **503** — the game loop did not answer within five seconds. Nothing is
  deleted; the shard is unwell and the log says so.

## Browsing the API

[Scalar](https://scalar.com) serves an interactive reference at `/scalar/v1`,
reading the OpenAPI document published at `/swagger/v1/swagger.json`. There is
also an unlisted `/health` returning `{"status":"ok"}`, for a load balancer or
a container probe.

## Adding endpoints

An endpoint group implements `IApiEndpointRegistration` and is registered in
`MoongateApiEndpointsPlugin` with `RegisterApiEndpoint<T>()`; the server
resolves every group and maps it at startup. This is the same registration seam
used by packet handlers and event subscribers, and each of those has its own
plugin too.

A group that is never registered is not a startup error: the server comes up
and the route simply 404s, and Scalar shows a reference with nothing in it.

Groups live in `Moongate.Server`, not in `Moongate.Http.Plugin`: `Program.cs`
adds that plugin, so it cannot reference the server back. The plugin owns the
plumbing — config, tokens, the server itself — and the game owns the endpoints
that need game services.

Anything that mutates world state must get onto the game loop first; see
[Architecture overview](architecture.md). None of the v1 endpoints do.
