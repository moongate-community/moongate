# Scheduled Events

Moongate supports Lua-first scheduled events for shard-wide timed behavior.

This system is separate from NPC brain loops and separate from combat or dialogue events.

Use it for things like:

- town crier announcements
- daily or weekly world reminders
- holiday triggers
- scripted world-state changes

## Overview

Scheduled events are authored as Lua files under:

- `moongate_data/scripts/events/**`

Each script registers a single event definition through:

```lua
local scheduled_events = require("common.scheduled_events")
```

At runtime, the server:

1. loads the Lua definitions
2. validates recurrence and schedule fields
3. arms one-shot timers on top of `ITimerService`
4. publishes `ScheduledEventTriggeredEvent`
5. forwards the event to global Lua as:

```lua
function on_scheduled_event(event)
end
```

## Example

```lua
local scheduled_events = require("common.scheduled_events")

return scheduled_events.event("town_crier_morning", {
    trigger_name = "town_crier_announcement",
    recurrence = "daily",
    time = "09:00",
    time_zone = "Europe/Rome",
    payload = {
        message = "Hear ye! The bank is now open."
    }
})
```

## Supported Recurrence Types

Current v1 support:

- `once`
- `daily`
- `weekly`
- `monthly`

### Once

```lua
return scheduled_events.event("server_launch", {
    trigger_name = "launch_announcement",
    recurrence = "once",
    start_at = "2026-03-20T18:00:00Z"
})
```

### Daily

```lua
return scheduled_events.event("daily_noon", {
    trigger_name = "daily_ping",
    recurrence = "daily",
    time = "12:00"
})
```

### Weekly

```lua
return scheduled_events.event("market_days", {
    trigger_name = "market_open",
    recurrence = "weekly",
    time = "09:00",
    days_of_week = { "monday", "wednesday", "friday" }
})
```

### Monthly

```lua
return scheduled_events.event("rent_due", {
    trigger_name = "rent_due_notice",
    recurrence = "monthly",
    time = "08:00",
    day_of_month = 1
})
```

## Definition Fields

- `trigger_name`
  - stable logical trigger identifier delivered in the fired event
- `recurrence`
  - one of `once`, `daily`, `weekly`, `monthly`
- `enabled`
  - optional, defaults to `true`
- `time`
  - required for `daily`, `weekly`, `monthly`
  - format `HH:mm`
- `time_zone`
  - optional
  - defaults to UTC
- `start_at`
  - required for `once`
  - ISO UTC timestamp recommended
- `days_of_week`
  - required for `weekly`
  - lowercase English names like `monday`
- `day_of_month`
  - required for `monthly`
  - value `1..31`
- `payload`
  - optional Lua table carried with the fired event

## Global Lua Callback

When a scheduled event fires, the global script bridge calls:

```lua
function on_scheduled_event(event)
    if event.trigger_name == "town_crier_announcement" then
        log.info("Scheduled event fired: " .. event.event_id)
    end
end
```

Available event fields:

- `event.event_id`
- `event.trigger_name`
- `event.scheduled_at_utc`
- `event.fired_at_utc`
- `event.recurrence_type`
- `event.payload`

## Notes

- This is a shard-level global event system, not a brain-local NPC timer system.
- `enabled = false` prevents the event from being armed.
- `time_zone` is applied when calculating recurring events.
- Recurring events reschedule themselves after firing.
