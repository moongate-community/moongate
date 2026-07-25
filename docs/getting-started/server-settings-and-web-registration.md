# Server settings & web registration

Moongate keeps a small, operator-editable **server profile** — a description,
contact links, uploadable visual assets, and a web-registration toggle — that a
public website or launcher can read, and that staff edit through the REST API.
Unlike [`moongate.yaml`](configuration.md), this profile is **persisted state**:
it lives in the save store and is changed at runtime over HTTP, not by editing a
file.

All routes below are served by the HTTP plugin (`http` section of
`moongate.yaml`); the admin routes require a staff **bearer token**
(`Administrator` or `GrandMaster`), obtained from `POST /api/v1/auth/login`. The
full request/response shapes render in the **API** reference.

## The public profile

Anyone can read the profile — this is what a shard website or launcher consumes:

```
GET /api/v1/server-info
```

```json
{
  "shardName": "Moongate",
  "description": "A friendly Trammel-rules shard.",
  "contacts": { "website": "https://example.com", "email": "gm@example.com", "discord": "https://discord.gg/xxxx" },
  "registrationEnabled": true,
  "assets": { "Logo": "/api/v1/server-info/assets/logo" }
}
```

`shardName` comes from the `moongate` config section; everything else comes from
the persisted profile. `assets` maps each populated slot to the URL that serves
its image.

## Editing the profile (staff)

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/v1/admin/server-settings` | Read the full settings. |
| `PUT` | `/api/v1/admin/server-settings` | Update settings; **every field is optional** and an omitted one is left unchanged. |

```bash
curl -X PUT https://your-shard:8933/api/v1/admin/server-settings \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{ "description": "A friendly shard.",
        "contacts": { "website": "https://example.com", "discord": "https://discord.gg/xxxx" } }'
```

## Visual assets

Three fixed named slots — **`logo`**, **`favicon`**, **`banner`** — each hold one
image file on disk under the runtime root (`web/assets/`); the profile stores only
the filename and content-type.

| Method | Route | Notes |
|---|---|---|
| `POST` | `/api/v1/admin/server-settings/assets/{slot}` | `multipart/form-data` upload (field `file`). Replaces any previous image in the slot. |
| `DELETE` | `/api/v1/admin/server-settings/assets/{slot}` | Removes the slot's image. |
| `GET` | `/api/v1/server-info/assets/{slot}` | **Public** — streams the image with its content-type. |

Uploads are validated: an unknown slot answers `400`, a non-image type `415`, and
a file over the size limit `413`. Accepted types are PNG, JPEG, WebP, SVG and ICO.

```bash
curl -X POST https://your-shard:8933/api/v1/admin/server-settings/assets/logo \
  -H "Authorization: Bearer $TOKEN" -F "file=@logo.png"
```

## Web registration

Registration is **disabled by default**. Opening it requires both the persisted
`registrationEnabled` toggle and all local email-verification prerequisites.
The public endpoints are:

```
POST /api/v1/register         { "username": "...", "password": "...", "email": "..." }
POST /api/v1/register/resend  { "username": "...", "email": "..." }
POST /api/v1/register/verify  { "token": "..." }
```

### Credential rules

Leading and trailing whitespace is removed from usernames and email addresses.
Password whitespace is significant and is not removed.

| Field | Rules |
|---|---|
| `username` | 3–30 ASCII letters, digits, `.`, `_` or `-`; it must not already be in use. |
| `password` | 8–30 printable ASCII characters (space through `~`). |
| `email` | Required, syntactically valid, and unique across active and pending accounts. Email uniqueness is case-insensitive. |

Duplicate usernames and email addresses share the same generic `409` response;
the response does not reveal which field matched.

### Verification lifecycle

1. `POST /api/v1/register` creates one inactive `Player` account, queues its
   verification email, and answers `202 Accepted`. The account cannot log in
   until it is verified.
2. The raw single-use token is a random 64-character hexadecimal value. Only its
   64-character SHA-256 hexadecimal hash is persisted. The raw value exists only
   long enough to enter the notification pipeline.
3. The token expires exactly 24 hours after creation. At the expiry instant it
   is already expired.
4. `POST /api/v1/register/verify` consumes a valid token, clears its persisted
   hash and expiry, activates the account, and answers `200`.

An expired token answers `410 Gone` and its state is cleared. An unknown,
already-used or blank token answers `400`. Verification does not depend on the
registration toggle or readiness, so an already-issued link still works after
an operator closes registration or an email prerequisite becomes unavailable.

Pending accounts created by an older version may still carry a plaintext token.
Those legacy tokens cannot activate an account: attempting to verify one answers
`410` and clears it. A matching resend rotates a legacy pending account to a new
hashed token with a fresh 24-hour lifetime.

### Resending without account enumeration

`POST /api/v1/register/resend` validates the username and email shapes, then
answers the same empty `202 Accepted` whether they match a pending account, an
active account or no account. Only a matching pending account gets a newly
rotated token and verification email. This prevents the response from becoming
an account-discovery oracle.

Resend is available even while the persisted registration toggle is off, so a
pending user can recover after registrations close. It still requires email
readiness: an unavailable prerequisite answers `503`. Malformed input answers
`400`, and a rate limit answers `429`.

### Abuse protection

The registration endpoint has a fixed-window limit per client IP. Resend has
separate fixed-window limits per client IP and per normalized username/email
target; the target key is hashed rather than stored in the limiter as raw
account data. The `http` keys below control the permit count and window for all
of these budgets.

Accounts also remain inactive until verification, so accepted but abandoned
registrations cannot log in.

### Readiness and the persisted toggle

The staff settings response keeps the operator's persisted
`registrationEnabled` value and adds a `registrationReadiness` object:

| Field | Meaning |
|---|---|
| `ready` | All three local prerequisites below are true. |
| `websiteValid` | `Contacts.Website` is an absolute `http` or `https` URL with a host. |
| `emailChannelSelected` | `notifications.AccountVerificationChannel` is `email` (case-insensitive). |
| `emailChannelAvailable` | The SMTP plugin registered the `email` channel at startup. |

By contrast, public `GET /api/v1/server-info` reports an **effective**
`registrationEnabled`: the persisted toggle must be true **and** readiness must
be true. The portal therefore hides registration when email verification cannot
be configured locally, even if the stored toggle remains on.

Enabling the toggle while readiness is false is rejected as a validation
problem. If readiness is lost after registration was enabled, new registration
and resend requests answer `503 Service Unavailable`; staff can still set the
persisted toggle to false. “Ready” covers local configuration only, not SMTP
server reachability or durable delivery; see
[Notifications](notifications.md#registration-readiness-and-delivery).

### Enable registration in this order

1. Configure the SMTP plugin in `<root>/plugins/configs/smtp.yaml`, including
   real `Host` and `FromAddress` values and the appropriate transport/security
   settings. Follow the [SMTP configuration and secrets
   guidance](notifications.md#sending-email).
2. Set `notifications.AccountVerificationChannel: email` in
   `<root>/moongate.yaml`.
3. Restart Moongate so both configuration files are loaded and the SMTP plugin
   can register the `email` channel. Confirm the startup log says account
   verification will use `email`.
4. Through `PUT /api/v1/admin/server-settings`, set `Contacts.Website` to the
   absolute HTTP(S) base URL of the player portal. Moongate appends the
   normalized `/verify?token=...` route to this base.
5. Read `GET /api/v1/admin/server-settings` and confirm all four
   `registrationReadiness` fields are `true`.
6. Only then set `registrationEnabled: true` with another staff settings update.

If a prerequisite fails later, disable the persisted toggle first, repair the
configuration, restart when a file-based setting changed, confirm readiness,
and re-enable it.

### HTTP responses

| Endpoint | Responses |
|---|---|
| `POST /api/v1/register` | `202` created pending; `400` invalid input; generic `409` duplicate username/email; `403` persisted toggle off; `429` rate-limited; `503` readiness unavailable. |
| `POST /api/v1/register/resend` | Generic `202` for matching or non-matching valid identities; `400` invalid input; `429` rate-limited; `503` readiness unavailable. |
| `POST /api/v1/register/verify` | `200` activated; `400` unknown/already-used token; `410` expired or legacy plaintext token. |

## `http` config keys

These live in the `http` section of `moongate.yaml`:

| Key | Type | Default | Meaning |
|---|---|---|---|
| `MaxAssetUploadBytes` | long | `2097152` (2 MB) | Maximum size of an uploaded asset. |
| `RegistrationRateLimitPermits` | int | `5` | Registration attempts allowed per window, per IP. |
| `RegistrationRateLimitWindowMinutes` | int | `10` | Length of the rate-limit window, in minutes. |

## Public statistics

`GET /api/v1/stats` — anonymous, like `/api/v1/server-info`. Where
`server-info` describes the shard's identity, this reports its numbers.

```json
{
  "generatedAt": "2026-07-21T08:31:00+00:00",
  "uptimeSeconds": 43200,
  "players":  { "online": 12, "connections": 15 },
  "accounts": { "total": 340, "active": 300, "characters": 512 },
  "world":    { "npcs": 1840, "items": 27310 },
  "content":  { "itemTemplates": 412, "mobileTemplates": 19 }
}
```

| Field | Meaning |
|---|---|
| `players.online` | Characters currently being played — who is actually in the world. |
| `players.connections` | Every open connection, including clients still at the login or character-select screen. |
| `accounts.total` | Accounts registered on the shard. |
| `accounts.active` | Accounts that are enabled — the flag web registration sets on verification. |
| `accounts.characters` | Characters created across all accounts, online or not. |
| `world.npcs` | Mobiles no account owns. |
| `world.items` | Persisted world items, including container contents. |
| `content.itemTemplates` | Item templates loaded at startup. |
| `content.mobileTemplates` | Mobile templates loaded at startup. |
| `uptimeSeconds` | Seconds since the server started. |
| `generatedAt` | When the snapshot was taken. `0001-01-01T00:00:00+00:00` means the world is not ready yet, so no snapshot has been computed. |

> [!NOTE]
> The figures are a **cached snapshot**, not a live read. Counting world
> entities has to happen on the game loop, so the server takes a first
> snapshot there as soon as the world is ready and recomputes it every
> `StatsRefreshSeconds` (30 by default); the endpoint serves the last one, up
> to that many seconds old. The response carries a matching
> `Cache-Control: public, max-age=…`, so a website polling the route cannot
> ask for data more often than it changes.
