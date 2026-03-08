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

## Frontend UI

When UI hosting is enabled (default), the server serves a React-based admin dashboard on `/`. The frontend source is in the `ui/` directory.

## Notes

- The HTTP server runs independently from the game TCP server.
- Endpoint availability depends on which services are registered at bootstrap.
- All mutating endpoints are protected by JWT when JWT is enabled.

---

**Previous**: [Event System](events.md) | **Next**: [Session Management](sessions.md)
