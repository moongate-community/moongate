# News API

Shard news managed over REST by the `Moongate.News.Plugin`. Staff create and edit news; anyone
can read the published ones (for a launcher or website).

A news entry is `{ id, title, body, author, publishedAt, updatedAt, isPublished }`. Drafts
(`isPublished: false`) are visible only through the staff routes.

## Public

- `GET /api/v1/news` — published news, newest first.
- `GET /api/v1/news/{id}` — one published entry (404 if it is missing or a draft).

## Staff

All require a JWT for an `Administrator` or `GrandMaster` account (the `admin` policy).

- `POST /api/v1/admin/news` — create. Body `{ "title", "body", "isPublished" }`; the author is
  taken from the token. Returns **201** with the created entry.
- `PUT /api/v1/admin/news/{id}` — update title, body and published state. **200** / **404**.
- `DELETE /api/v1/admin/news/{id}` — **204** / **404**.
- `GET /api/v1/admin/news` — every entry, drafts included, newest first.
- `GET /api/v1/admin/news/{id}` — one entry in any state.

## In the world

News reaches players in the game, not only over REST.

**Entering the world** shows the message of the day first — see below — and then the **newest
published** news entry as a system message. One entry, not the archive: a shard that has published
twenty things should not fire twenty lines at someone walking through the door. A shard with nothing
published says nothing at all — greeting a player with an empty line is worse than silence.

**`.news`** prints every published entry, and is where the rest lives. It is a `Player`-level
command, so anyone may run it, and it is available from the admin console and the REST console as
well as in game.

Drafts appear in neither. A player-facing surface that leaked one would publish it as surely as the
staff routes would, and from the one place nobody would think to look.

Both live in the news plugin, beside the service they read, rather than in the server — which cannot
reference a plugin. What made that possible is `IChatService.SendSystemMessage`, the single-session
sibling of `Broadcast`: the factory that builds these messages lives in `Moongate.Server`, so
without a seam on the contract assembly a plugin had no way to speak to one player.

## The message of the day

Before the news, a player entering the world is shown:

1. `Moongate v<version>` — the build the shard is running.
2. `Total online players: <n>` — how many characters are in the world, counted at that moment
   rather than at startup.
3. Whatever the operator wrote in **`motd.txt`**, one system message per line.

`motd.txt` sits at the root of the runtime directory, beside `moongate.yaml`, because that is where
an operator looks. It is written with a sample line at first boot — left to appear on its own, it
would show up only after somebody had logged in, by which point they had already been greeted
without it — and it is **read on each greeting**, so editing it takes effect without a restart.
Blank lines are dropped, so a file left with trailing newlines does not greet anyone with silence;
emptying it deliberately leaves just the first two lines.

The order is pinned by a test rather than assumed. The message of the day and the news greeting are
two independent subscribers on the same event, and nothing in the event bus documents that handlers
run in subscription order — so the shard introducing itself before it hands out announcements is
asserted, not hoped for.
