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
