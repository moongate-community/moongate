# Create Your First Lua Admin Command

This guide creates one in-game GameMaster command entirely in Lua.

By the end, you will:

- create a command file under `~/moongate/scripts/commands/gm/`
- register it in `~/moongate/scripts/commands/gm/init.lua`
- run it in game with the leading `.` prefix

## Before You Start

You need:

- your shard root is `~/moongate`
- your server is configured to run against `~/moongate`
- you can log in with a GameMaster-capable account

## Step 1: Create The Command File

Create:

```text
~/moongate/scripts/commands/gm/tutorial_ping.lua
```

Paste this content:

```lua
command.register("tutorial_ping", function(ctx)
    local args = ctx.arguments or {}
    local target = args[1] or "traveler"

    speech.broadcast("Lua tutorial command says hello to " .. target .. ".")
    ctx:print("tutorial_ping executed for " .. target .. ".")
end, {
    description = "Tutorial Lua admin command.",
    minimum_account_type = "GameMaster"
})
```

What this does:

- registers a command named `tutorial_ping`
- reads the first argument from `ctx.arguments`
- broadcasts a server-wide message
- prints a private confirmation line back to the caller

## Step 2: Register The Command In `commands/gm/init.lua`

Open:

```text
~/moongate/scripts/commands/gm/init.lua
```

Add this line with the other GM command requires:

```lua
require("commands.gm.tutorial_ping")
```

## Step 3: Restart The Server

Restart the server so the new command file and the updated init file are both loaded.

## Step 4: Run The Command In Game

In game, type:

```text
.tutorial_ping moongate
```

Expected result:

- the shard broadcasts `Lua tutorial command says hello to moongate.`
- you also see `tutorial_ping executed for moongate.`

## Common Mistakes

- Forgetting the `require("commands.gm.tutorial_ping")` line
- Using `ctx.arguments[0]` instead of `ctx.arguments[1]`
- Expecting the command to work without the leading `.` when run in game
- Setting `minimum_account_type` lower than you intended for an operator command

## Next Step

Continue with [Create Your First Plugin](../architecture/create-your-first-plugin.md) and then
[Create Your First C# Admin Command](../architecture/create-your-first-csharp-admin-command.md).
