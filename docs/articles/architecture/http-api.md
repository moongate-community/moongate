# HTTP API

Moongate v2 embeds an ASP.NET Core HTTP server for administration, monitoring, and diagnostics.

## Overview

The HTTP service (`MoongateHttpService`) starts alongside the game server and exposes REST endpoints for server management. It supports OpenAPI documentation via Scalar UI.

## Configuration

HTTP server settings in `moongate.json`:

```json
{
  "http": {
    "enabled": true,
    "port": 4080,
    "enableSwagger": true,
    "enableJwt": false,
    "jwtSecret": ""
  }
}
```

When `enableJwt` is true, protected endpoints require a Bearer token obtained from `/auth/login`.

## Endpoints

### System

| Method | Path | Description |
|--------|------|-------------|
| GET | `/health` | Health probe (always returns 200) |
| GET | `/metrics` | Prometheus-format metrics |
| GET | `/api/version` | Server version metadata |
| GET | `/scalar` | OpenAPI interactive documentation |

### Authentication (JWT)

| Method | Path | Description |
|--------|------|-------------|
| POST | `/auth/login` | Authenticate and receive JWT token |

### Player Portal

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/portal/me` | Return the authenticated account and its characters |
| PUT | `/api/portal/me` | Update editable profile fields for the authenticated account |
| PUT | `/api/portal/me/password` | Change the authenticated account password |

### Users

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/users` | List all user accounts |
| GET | `/api/users/{accountId}` | Get user by account ID |
| POST | `/api/users` | Create new user account |
| PUT | `/api/users/{accountId}` | Update user account |
| DELETE | `/api/users/{accountId}` | Delete user account |

### Sessions

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/sessions/active` | List active in-game sessions |

### Help Tickets

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/help-tickets` | List persisted help tickets for staff with server-side pagination and filters |
| GET | `/api/help-tickets/{ticketId}` | Get one persisted help ticket for staff |
| PUT | `/api/help-tickets/{ticketId}/assign-to-me` | Assign a help ticket to the authenticated staff account |
| PUT | `/api/help-tickets/{ticketId}/status` | Update a help ticket status for staff |
| GET | `/api/help-tickets/me` | List the authenticated player's open or assigned tickets |

### Commands

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/commands/execute` | Execute a console command |

### Item Templates

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/item-templates` | Paged search of item templates |
| GET | `/api/item-templates/{id}` | Get item template by ID |
| GET | `/api/item-templates/by-item-id/{itemId}/image` | Get item art image |

## Functional Usage Examples

Base URL used in examples:

```bash
BASE_URL="http://localhost:4080"
```

### Health + Version

```bash
curl -s "$BASE_URL/health"
curl -s "$BASE_URL/api/version"
```

Expected:

- `/health` returns plain text `ok`
- `/api/version` returns JSON:

```json
{
  "version": "x.y.z",
  "codename": "..."
}
```

### JWT Login (when enabled)

```bash
curl -s -X POST "$BASE_URL/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "admin"
  }'
```

Response shape:

```json
{
  "accessToken": "<jwt>",
  "tokenType": "Bearer",
  "expiresAtUtc": "2026-03-09T12:34:56+00:00",
  "accountId": "1",
  "username": "admin",
  "role": "Administrator"
}
```

If JWT is enabled, pass token:

```bash
TOKEN="<jwt>"
AUTH_HEADER="Authorization: Bearer $TOKEN"
```

### Player Portal Account Snapshot

The player-facing portal uses the same JWT login endpoint, but reads only the authenticated account attached to the token.

```bash
curl -s "$BASE_URL/api/portal/me" -H "$AUTH_HEADER"
```

Response shape:

```json
{
  "accountId": "1",
  "username": "player_one",
  "email": "player@example.com",
  "accountType": "Regular",
  "characters": [
    {
      "characterId": "2",
      "name": "Lilly",
      "mapId": 0,
      "x": 1324,
      "y": 1624
    }
  ]
}
```

### Player Portal Password Change

The portal profile page can change the password of the authenticated account.

- `Regular` accounts must provide the current password.
- `GameMaster` and `Administrator` accounts may omit the current password.
- Successful changes clear `RecoveryCode`.

```bash
curl -s -X PUT "$BASE_URL/api/portal/me/password" \
  -H "$AUTH_HEADER" \
  -H "Content-Type: application/json" \
  -d '{
    "currentPassword": "old-secret",
    "newPassword": "new-secret",
    "confirmPassword": "new-secret"
  }'
```

Response:

- `200 OK` on success
- `400 Bad Request` on validation failure
- `401 Unauthorized` when JWT is missing or invalid

### Item Template Search (page/pageSize + name/tag)

```bash
curl -s "$BASE_URL/api/item-templates?page=1&pageSize=20&name=door&tag=flippable" \
  -H "$AUTH_HEADER"
```

Response shape:

```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 123,
  "items": [
    {
      "id": "dark_wood_door",
      "name": "Dark Wood Door",
      "category": "doors",
      "itemId": 1692,
      "params": {
        "facing": { "type": "string", "value": "WestCW" }
      }
    }
  ]
}
```

Notes:

- `page <= 0` is normalized to `1`
- `pageSize <= 0` defaults to `50`
- `pageSize` is clamped to max `200`

### Item Template Image by `itemId`

`itemId` must be in `0x...` format.

```bash
curl -s "$BASE_URL/api/item-templates/by-item-id/0x069C/image" \
  -H "$AUTH_HEADER" \
  --output item.png
```

Behavior:

- if cached image exists under `images/items/`, returns it
- otherwise generates PNG from art data and caches it
- returns `404` if art entry does not exist

### Active Sessions

```bash
curl -s "$BASE_URL/api/sessions/active" -H "$AUTH_HEADER"
```

Response shape:

```json
[
  {
    "sessionId": 2,
    "accountId": "1",
    "username": "admin",
    "accountType": "Administrator",
    "characterId": "2",
    "characterName": "tommy"
  }
]
```

### Help Ticket Admin Queue

All staff ticket endpoints require an authenticated administrative user when JWT is enabled.

Paged staff queue:

```bash
curl -s "$BASE_URL/api/help-tickets?page=1&pageSize=20&status=Open&assignedToMe=false" \
  -H "$AUTH_HEADER"
```

Response shape:

```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 42,
  "items": [
    {
      "ticketId": "1001",
      "senderCharacterId": "2",
      "senderAccountId": "1",
      "category": "Question",
      "message": "I am stuck near Britain bank.",
      "status": "Open",
      "mapId": 0,
      "x": 1496,
      "y": 1628,
      "z": 20,
      "createdAtUtc": "2026-03-19T10:15:00Z",
      "assignedAtUtc": null,
      "closedAtUtc": null,
      "lastUpdatedAtUtc": "2026-03-19T10:15:00Z",
      "assignedToCharacterId": "",
      "assignedToAccountId": ""
    }
  ]
}
```

Supported query string parameters for `GET /api/help-tickets`:

- `page` defaults to `1` when omitted or invalid
- `pageSize` defaults to `50` and is clamped to `200`
- `status` accepts `Open`, `Assigned`, `Closed`
- `category` accepts `Question`, `Stuck`, `Bug`, `Account`, `Suggestion`, `Other`, `VerbalHarassment`, `PhysicalHarassment`
- `assignedToMe=true` filters by the authenticated staff account

Single ticket detail:

```bash
curl -s "$BASE_URL/api/help-tickets/1001" -H "$AUTH_HEADER"
```

Take ownership:

```bash
curl -s -X PUT "$BASE_URL/api/help-tickets/1001/assign-to-me" \
  -H "$AUTH_HEADER"
```

Update status:

```bash
curl -s -X PUT "$BASE_URL/api/help-tickets/1001/status" \
  -H "$AUTH_HEADER" \
  -H "Content-Type: application/json" \
  -d '{
    "status": "Closed"
  }'
```

Player-facing ticket list:

```bash
curl -s "$BASE_URL/api/help-tickets/me" -H "$AUTH_HEADER"
```

The `/me` endpoint returns the authenticated player's currently open or assigned tickets.

### Execute Console Command via HTTP

```bash
curl -s -X POST "$BASE_URL/api/commands/execute" \
  -H "Content-Type: application/json" \
  -H "$AUTH_HEADER" \
  -d '{ "command": "spawn_doors" }'
```

Response shape:

```json
{
  "success": true,
  "command": "spawn_doors",
  "outputLines": [
    "Starting door generation...",
    "Door generation finished in 36 seconds"
  ],
  "timestamp": 1772448523355
}
```

## Frontend UI

When UI hosting is enabled (default), the server serves a React-based admin dashboard on `/`. The frontend source is in the `ui/` directory.

Player-facing routes are exposed separately:

- `/portal/login`
- `/portal/account`
- `/portal/profile`

## Notes

- The HTTP server runs independently from the game TCP server.
- Endpoint availability depends on which services are registered at bootstrap.
- All mutating endpoints are protected by JWT when JWT is enabled.

---

**Previous**: [Event System](events.md) | **Next**: [Session Management](sessions.md)
