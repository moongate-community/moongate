# Create Your First Scheduled Event

This guide creates one shard-level event that fires once at a specific UTC timestamp.

By the end, you will:

- create a Lua file under `~/moongate/scripts/events/`
- add a global `on_scheduled_event(event)` callback
- start the server and watch the event fire

## Before You Start

You need:

- your shard root is `~/moongate`
- your server is configured to run against `~/moongate`
- you can restart the server
- you can see the server log output while the shard is running

## Step 1: Create The Event File

Create this folder if it does not exist yet:

```text
~/moongate/scripts/events/
```

Inside it, create:

```text
~/moongate/scripts/events/tutorial_ping_once.lua
```

Paste this content:

```lua
local scheduled_events = require("common.scheduled_events")

return scheduled_events.event("tutorial_ping_once", {
    trigger_name = "tutorial_ping",
    recurrence = "once",
    start_at = "REPLACE_WITH_A_UTC_TIMESTAMP_2_MINUTES_IN_THE_FUTURE"
})
```

Important note:

- `start_at` must be a real UTC timestamp in the future, for example `2026-03-26T14:30:00Z`
- if the timestamp is already in the past when the server starts, the event will not fire

## Step 2: Add The Global Callback

Open:

```text
~/moongate/scripts/init.lua
```

Add this function below the other global callbacks:

```lua
function on_scheduled_event(event)
    if event == nil or event.trigger_name ~= "tutorial_ping" then
        return
    end

    log.info("Tutorial scheduled event fired: " .. tostring(event.event_id))
    speech.broadcast("Tutorial scheduled event fired.")
end
```

Why this step matters:

- the event definition schedules the timer
- the global callback is where you react when the timer fires

## Step 3: Double-Check The Timestamp

Before starting the server, make sure `start_at` is still in the future.

For a first test, give yourself around two minutes of buffer so you have time to restart the shard and watch the log.

## Step 4: Restart The Server

Restart the server after saving both files.

The scheduled event service loads every `.lua` file under `~/moongate/scripts/events/` when it starts.

## Step 5: Wait For The Event To Fire

Leave the server running until the `start_at` time passes.

Expected result:

- the server log prints `Tutorial scheduled event fired: tutorial_ping_once`
- connected players receive the broadcast `Tutorial scheduled event fired.`

## Common Mistakes

- Using a timestamp that is already in the past
- Creating the event file outside `~/moongate/scripts/events/`
- Forgetting to add `on_scheduled_event(event)` in `~/moongate/scripts/init.lua`
- Restarting the server after the target time has already passed

## Next Step

Continue with [Create Your First Gump](create-your-first-gump.md).
