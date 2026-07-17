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
code path asks for it. One plugin is skipped: `MoongateHttpPlugin`, which
owns the API — the plumbing and every endpoint group the server would map.

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
| `GET`    | `/api/v1/player/me/characters`       | `player` | the caller's own characters        |
| `GET`    | `/api/v1/admin/characters`           | `admin`  | every character, paged             |
| `GET`    | `/api/v1/images/items/{id}.png`      | none     | item art as PNG, or 400 / 404 / 503 |
| `POST`   | `/api/v1/admin/images/items`         | `admin`  | 202, or 409 / 503                  |
| `GET`    | `/api/v1/admin/images/items`         | `admin`  | export progress                    |
| `GET`    | `/api/v1/images/maps`                | none     | the facets and their tile grids    |
| `GET`    | `/api/v1/images/maps/{map}/full.png` | none     | a whole facet as one PNG           |
| `GET`    | `/api/v1/images/maps/{map}/{z}/{x}/{y}.png` | none | one map tile                  |
| `POST`   | `/api/v1/admin/images/maps`          | `admin`  | 202, or 409 / 503                  |
| `GET`    | `/api/v1/admin/images/maps`          | `admin`  | pre-warm progress                  |

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

## Characters

`GET /api/v1/player/me/characters` reports the caller's own characters — stats,
appearance and where they stand. The account comes from the token, never from the
request: an id a caller could supply would let anyone list anyone's characters by
changing a number. It is not paged, because an account holds a handful of
characters and a page envelope around five rows is ceremony.

`GET /api/v1/admin/characters` is the staff view of every character, paged and
searchable. `search` is free text matched against both the character's name and
the owning account's username, so "find me so-and-so's character" is one query.

Nothing on a mobile marks it as a player character: the only link is the owning
account's `MobileIds`. The admin route therefore reads the accounts first, which
it must do anyway to name each character's owner, and uses that to tell characters
from the NPCs sharing the mobile store.

Neither route returns `MobileEntity`. It carries `BrainScriptId`, `LootTableId`
and `BackpackId`, and a field added to it later would publish itself; the DTO
names its fields for the same reason `AccountResponse` does.

There is no fame or karma. `ITitleService` takes both, but nothing stores them on
the entity yet, and a field pinned at 0 in this reference would be worse than an
absent one. They arrive with the system that moves them.

One detail worth copying if you add a route that reads the caller's identity: the
account id is read from `NameIdentifier` before `sub`. `JwtTokenService` writes it
into `sub`, but JwtBearer's inbound claim mapping is on by default and renames it
before the principal reaches the route — reading only `sub` 401s every request.

## Item templates

Staff-only, read-only views over the item template registry — the YAML-born
catalog every item is built from.

`GET /api/v1/admin/items/templates` lists templates as paged summary rows,
using the shared paging contract: `page` is 1-based, `pageSize` caps at 100,
and `search` is free text matched case-insensitively against a template's id,
name, category and tags. Each row carries an `imageUrl` pointing at the item
art route, so a table can show the sprite without computing anything.

`GET /api/v1/admin/items/templates/{id}` returns one full template, specs
included — equip, weapon, container, book and script params exactly as the
[data reference](../scripting/data/item-templates.md) describes them. Ids are
case-insensitive; an unknown id answers 404.

Templates are read-only over the API on purpose: they are born from YAML and
reloaded at startup, so a REST write would only diverge from the source of
truth until the next restart.

## Item images

`GET /api/v1/images/items/0x1234.png` returns an item's art as a PNG, and is
open without a token: the art is client data every player already has on disk.
The id is hex with or without the `0x` prefix — a bare `1234.png` is the same
item, not item 4660. Add `?hue=0x21` for a coloured variant; the range is 0 to
3000, and 0, or omitting it, gives the raw art.

The hue range is checked here rather than left to Ultima because `Hues.GetHue`
never fails: it masks the index and falls back to hue 0, so an out-of-range hue
would answer 200 with the wrong image instead of an error.

Images are decoded on first request and cached under `cache/images/items` in the
runtime root, so the first call for an item is slower than every call after it.
The directory is registered through `DirectoriesConfig` and needs no ignore rule
of its own: the whole runtime root is already outside git.

`POST /api/v1/admin/images/items` warms the cache for every item at once. It is
staff-only, answers 202, and works in the background; `GET` on the same route
reports progress, and a second `POST` while one runs answers 409. It generates
unhued art only — hued variants are left to the public route, since multiplying
every item by 3000 hues is not a cache but a disk filler. Progress lives in
memory and does not survive a restart, which is what a cache warm should do: the
lazy path is always the fallback.

One thing to know before adding any route that reads the client files:
`Moongate.Ultima`'s `Art` holds no locks, and shares an LRU bitmap cache, plain
dictionaries and a static scratch buffer across calls. `ItemImageService`
funnels every decode through a single gate for that reason — not one gate per
item, since two requests for *different* items corrupt that state exactly as two
for the same one would. Cache hits skip the gate entirely. Do not call `Art` from
a request thread without going through that service.

## Paged listings

Listing routes share one contract, in `PageRequest` and `PagedResponse`:

| Parameter | Default | Rules |
| --- | --- | --- |
| `page` | 1 | 1-based. Below 1 is a 400. |
| `pageSize` | 25 | 1 to 100. Outside that is a 400. |
| `search` | none | Free text. Blank means no filter. |

The response carries `items`, `total`, `page`, `pageSize` and `totalPages`, where
`total` counts everything the search matched and not just this page.

Out-of-range values are rejected rather than corrected. A caller asking for page
0, or for 5000 rows, has a bug: serving page 1 or 100 rows hides it, and in the
second case leaves them believing they read everything they asked for.

An empty page is a 200 with `total: 0`, never a 404. The query ran and matched
nothing, which is a fact rather than a failure.

Under it, `IEntityStore.QueryPaged` filters and orders inside the store's lock and
clones only the page. Do not page over `GetAll()` or `Query()` — both deep-clone
the entire store on every call, so paging over them costs the whole bucket per
page. `QueryPaged` requires its sort key explicitly, because paging an unordered
bucket lets a page repeat or drop a row with nothing in the response admitting it.

## Map images

`GET /api/v1/images/maps/{map}/{z}/{x}/{y}.png` serves a facet as 256×256 slippy-map
tiles, open without a token: like the item art, it is client data every player
already has. Facets are named, case-insensitively — `felucca`, `trammel`,
`ilshenar`, `malas`, `tokuno`, `termur`.

Zoom counts **up to detail**: `0` is the whole facet in a single tile, and `maxZoom`
is one pixel per map tile. `maxZoom` differs per facet — it is
`ceil(log2(longest side / 256))`, so Felucca's is 5 — and it depends on the shard's
own client files. Read `GET /api/v1/images/maps` and configure the viewer from what
it reports rather than hardcoding it.

`GET /api/v1/images/maps/{map}/full.png` is the whole facet in one image, for
downloading or printing rather than browsing.

`POST /api/v1/admin/images/maps` builds every tile and whole-facet image ahead of
time; `GET` on the same route reports progress. Staff-only. Run it before anyone
opens a viewer: the first request at a low zoom otherwise builds every tile beneath
it.

### How a tile is built, and why

A tile at native zoom is one `Map.GetImage` of exactly **32 blocks** — which is why
256 was chosen: a block is 8×8 tiles, so a native tile is a whole number of blocks
and never straddles one. Every zoom below is composed by halving its four children,
recursively, each cached on the way down.

The alternative — rendering a low zoom directly — means a facet-sized bitmap:
6144×4096 for Felucca, roughly 100 MB. Composing from children never holds more than
a few 256×256 images. `full.png` pays that cost once, unavoidably, because the
output *is* that image; the pre-warm exists so nobody meets it on a request.

**Fewer than four children is normal.** Grids do not double cleanly — each level's
size is its own `ceil` — so Felucca's z1 is 2×1 and its z0 tile asks for two children
that do not exist. A missing child leaves its quadrant transparent.

### The rule that matters

**Nothing may touch `Art`, `Map`, `RadarCol` or `TileData` outside
`IUltimaReadGate.ReadAsync`.**

`Map.cs` holds 18 locks of its own, which makes it look safe. It is not: it calls
`Art.IsValidStatic`, which reads a shared LRU cache, moves a **shared file-index
stream position** and writes a **shared static scratch buffer** — and `Art` holds no
locks at all. `RadarCol` holds none either. So map rendering and item art decoding
corrupt each other unless they queue behind the same gate, and it must be *one*
gate: a service with its own semaphore is exactly as broken as one with none.

None of this is visible from those types' signatures, which is why it is written
down here.

## Mobile images

`GET /api/v1/images/bodies/{body}.png` serves a body's idle, front-facing
frame, lazily rendered from the animation files and cached on disk; `hue`
gives a skin-hued variant its own cache entry. Bodies with no usable
animation answer 404. Like the item art, these are anonymous: it is client
data every player already has.

`GET /api/v1/images/hair/{style}.png` renders a hair style over a reference
body (400 unless `body` says otherwise); `facial=true` switches to beards,
`hue` dyes. `GET /api/v1/images/mobiles/templates/{id}.png` serves the
dressed figure of a mobile template — body, hair and worn equipment
composited in draw order. Hue specs resolve to their low end so the image is
deterministic and cacheable.

`GET /api/v1/images/mobiles/templates/{id}/paperdoll.png` serves the same
template as the classic client paperdoll instead: gump art, not an animation
frame — background, body, hair and equipment composited in their own draw
order. Pass `background=false` to drop the backdrop. A template with
`Gender: Random` renders as male, the same "pick one and stick to it"
determinism hue ranges get from `LowestHue`.

Three staff routes support the pickers and the cache:
`GET /api/v1/admin/bodies` pages the classified bodies (mobtypes.txt,
Equipment excluded, decimal or hex search); `GET /api/v1/admin/hair-styles`
lists the selectable styles; `POST /api/v1/admin/images/bodies` warms the
body cache in the background, polled with GET on the same route — the same
contract as the item and map exports.

## Browsing the API

[Scalar](https://scalar.com) serves an interactive reference at `/scalar/v1`,
reading the OpenAPI document published at `/swagger/v1/swagger.json`. There is
also an unlisted `/health` returning `{"status":"ok"}`, for a load balancer or
a container probe.

The prose on each route is not written in the reference; it is written in the
code. Swashbuckle reads the `///` off each handler, so a route's `<summary>`
becomes its summary and its `<remarks>` becomes its description. The XML those
are compiled into is produced by `GenerateDocumentationFile`, which is on for
every configuration precisely so the reference reads the same in development as
in production.

## Adding endpoints

An endpoint group implements `IApiEndpointRegistration` and is registered in
`MoongateHttpPlugin` with `RegisterApiEndpoint<T>()`; the server
resolves every group and maps it at startup. This is the same registration seam
used by packet handlers and event subscribers, and each of those has its own
plugin too.

A group that is never registered is not a startup error: the server comes up
and the route simply 404s, and Scalar shows a reference with nothing in it.

Every group lives in `Moongate.Http.Plugin`. Groups that need game services
take them from `Moongate.Server.Abstractions` — the contract assembly the
server implements — so the plugin never references the server itself. Groups
that need only the client files reach them through `Moongate.Ultima`, which
references no Moongate project.

This is also why the server finds the XML to document your routes with by
reading the DI registrations: it cannot name the assembly your group lives in,
so it asks the container which assemblies contributed endpoints and looks for
their XML beside the binaries.

Map each route to a **method group**, not a lambda:

```csharp
group.MapGet("/{username}", Get).WithName("GetAccount");

/// <summary>Fetches a single account by username.</summary>
/// <remarks>Answers 404 when no account carries that username.</remarks>
private IResult Get(string username) => ...;
```

A lambda has no method for the `///` to hang off, so the route documents itself
as blank — the reference still renders, and simply says nothing about it.

Write the `<summary>` and `<remarks>` for whoever is calling the route: what it
does, what it needs, what the failures mean. Reasoning aimed at whoever
maintains the handler goes in a plain `//` comment inside the method, which is
never published. The two registers are easy to mix up, and the cost of mixing
them up is internal notes appearing in the public reference.

Anything that mutates world state must get onto the game loop first; see
[Architecture overview](architecture.md). None of the v1 endpoints do.
