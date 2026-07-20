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
  -d '{ "description": "A friendly shard.", "registrationEnabled": true,
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

Registration is **disabled by default**. Turn it on by setting
`registrationEnabled: true` (see the `PUT` above). While enabled, the public
endpoint creates accounts:

```
POST /api/v1/register        { "username": "...", "password": "...", "email": "..." }
POST /api/v1/register/verify  { "token": "..." }
```

The flow:

1. `POST /api/v1/register` validates the input (username free, password present,
   **email required and well-formed**), then creates an **inactive** `Player`
   account carrying a single-use verification token, and answers `202 Accepted`.
   The account **cannot log in until it is verified**.
2. `POST /api/v1/register/verify` consumes the token, activates the account, and
   answers `200`. The token is single-use — a consumed or unknown token answers
   `400`.

Responses: `403` when registration is disabled, `409` for a taken username, `400`
for a missing/invalid field, and `429` when the caller is rate-limited.

> [!NOTE]
> **Email sending is not part of this feature yet.** Registration is *predisposed*
> for email verification: it creates the token and raises an
> `AccountRegistrationRequestedEvent` (carrying the account, email and token) that
> a future email feature will subscribe to in order to send the verification link.
> Until then no mail is sent — the token is written to the server log, and the
> verify endpoint works with whatever token you hold.

### Abuse protection

Two guards, on top of the off-by-default toggle:

- Accounts are inactive until verified, so spam produces only inert records.
- `POST /api/v1/register` is **rate-limited per client IP** (fixed window).

## `http` config keys

These live in the `http` section of `moongate.yaml`:

| Key | Type | Default | Meaning |
|---|---|---|---|
| `MaxAssetUploadBytes` | long | `2097152` (2 MB) | Maximum size of an uploaded asset. |
| `RegistrationRateLimitPermits` | int | `5` | Registration attempts allowed per window, per IP. |
| `RegistrationRateLimitWindowMinutes` | int | `10` | Length of the rate-limit window, in minutes. |
