# event

Shows the [seasonal events](../schedule.md#seasonal-events) and forces one on, off, or back to its
dates.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `event [list]` | Yes | Yes | Administrator | Game |
| `event on <id>` | Yes | Yes | Administrator | Game |
| `event off <id>` | Yes | Yes | Administrator | Game |
| `event auto <id>` | Yes | Yes | Administrator | Game |

```text
.event list
halloween: Halloween, 10-20 to 11-02, mode auto, active
winter: Winter, 12-20 to 01-06, mode auto, inactive

.event off halloween
Event halloween is now off.
```

`on` and `off` override the dates until `auto` gives the event back to them; the mode is kept
across restarts. A change that starts or ends the event calls its `on_start` or `on_end`. An id
that is not in `data/schedule.toml` prints `No event is called x. Events: halloween, winter`.
