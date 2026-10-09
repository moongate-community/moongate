# Schedule

`schedule.toml` is the calendar of [timed tasks and seasonal events](../schedule.md): a shutdown with
warnings, a message to everybody, a call to a Lua function, and the dates of events such as
Halloween.

```toml
[[task]]
id = "nightly_restart"
when = { every = "day", at = "04:00" }
action = "shutdown"
warnings = [600, 300, 60, 10]

[[event]]
id = "halloween"
name = "Halloween"
from = "10-20"
to = "11-02"
```

| Field | Meaning |
| --- | --- |
| `[[task]]` | One per timed task: `id`, `when` (`every`, `at`, `days`), `action` and what the action needs. |
| `[[event]]` | One per seasonal event: `id`, `name`, `from` and `to` as `MM-dd`, both inclusive. |

The shipped file has the tasks as examples in comments, so none runs until the operator enables
one, and the events `halloween` and `christmas` on ([Holidays](../holidays.md)). The file may be missing: the
calendar is then empty.

## Validation at startup

The server stops at startup, naming the file and the entry, when:

- a key is unknown, or an `id` is missing, repeated (tasks and events share the namespace) or not
  `[a-z0-9_]+` of at most 40 characters;
- `when.every` is not `hour`, `day` or `week`, `at` is not a valid `HH:MM` (or `:MM` for `hour`), or
  `days` names a day that is not `mon` to `sun`;
- the `action` is unknown, a `broadcast` has both `message` and `text` or none, a `lua` task has no
  `script`, or `warnings` are not descending seconds from 1 to 86400;
- an event has a `from` or `to` that is not a real `MM-dd` date.

## See also

- [Schedule](../schedule.md)
- [`event`](../commands/event.md)
- [Server configuration](../server-configuration.md) for the time zone
