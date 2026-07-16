# account

Creates and manages accounts. Backed by `AccountModule`.

Accounts are addressed by **username** — the handle a player logs in with —
rather than by serial, which no operator ever has to hand. Usernames are unique
across the shard.

> [!IMPORTANT]
> These functions grant shard access. `account.create` with an
> `account_level_type.Administrator` level makes a full administrator, and
> [`account.delete`](#accountdelete) destroys characters. Treat any script
> calling them as privileged.

The password you pass is hashed before it is stored; the hash is never handed
back to Lua.

## account.create

```lua
account.create(username, password, email, level) -> boolean
```

Creates an **active** account and returns `true` when it was created. All four
arguments are required — pass `nil` for `email` when you have none. `level`
takes an [`account_level_type`](enums.md#account_level_type) constant or its
name; an unrecognised level falls back to `Player`.

Returns `false` and logs a warning when the username is already taken, or when
the username or password is blank. The account can log in immediately.

**Example**

```lua
account.create("tom", "hunter2", "tom@example.com", account_level_type.Administrator)
```

## account.get

```lua
account.get(username) -> table or nil
```

Returns the account's fields, or `nil` when no account has that username. The
password hash is never included.

| Key | Type | Meaning |
|---|---|---|
| `id` | number | The account's serial. |
| `username` | string | The login handle. |
| `email` | string or nil | The address on file. |
| `level` | string | The [`account_level_type`](enums.md#account_level_type) name. |
| `is_active` | boolean | False when the account is blocked from logging in. |
| `mobiles` | table | Array of the serials of the account's characters. |

**Example**

```lua
local a = account.get("tom")
if a then
  log.info(a.username .. " has " .. #a.mobiles .. " characters")
end
```

## account.list

```lua
account.list() -> table of usernames
```

Returns every account's username as an array-table.

## account.exists

```lua
account.exists(username) -> boolean
```

True when an account answers to that username.

## account.set_password

```lua
account.set_password(username, password) -> boolean
```

Replaces the account's password. Returns `false` on an unknown username or a
blank password, leaving the old password working.

## account.set_level

```lua
account.set_level(username, level) -> boolean
```

Sets the account's privilege level, taking an
[`account_level_type`](enums.md#account_level_type) constant or its name.
Returns `false` on an unknown username or an unrecognised level — unlike
[`account.create`](#accountcreate), which falls back to `Player`.

## account.set_active

```lua
account.set_active(username, is_active) -> boolean
```

Blocks (`false`) or unblocks (`true`) the account. A blocked account is refused
at login even with the right password, but keeps its characters, so blocking is
reversible where [`account.delete`](#accountdelete) is not. Returns `false` on
an unknown username.

## account.delete

```lua
account.delete(username) -> boolean
```

Deletes the account **along with every character it owns** and everything those
characters carry — equipment, containers and their contents. Returns `true` when
it went.

Returns `false` and logs the reason when the username is unknown, or when any of
the account's characters is being played right now: the delete is refused as a
whole rather than half-applied, so the account never ends up stripped of every
character but the one still in play. Ask the player to log out first.

This is the one `account` function that touches world state, so it must run on
the game-loop thread — inside an [`events.on`](events.md) handler, or via
[`game.post`](game.md#gamepost). Called elsewhere it still works, but logs a
warning.

**Example**

```lua
game.post(function()
  if not account.delete("tom") then
    log.warn("could not delete tom")
  end
end)
```
