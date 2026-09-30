# shutdown

Stops the server gracefully, at once or after a delay.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `shutdown [seconds]` | Yes | Yes | Administrator | Game |

```text
shutdown
shutdown 60
```

In-game administrators use `.shutdown` or `.shutdown 60`. With no argument or `0`,
the server announces `The server is shutting down now.` (message 30016) and requests
graceful shutdown. A positive number announces `The server will shut down in <seconds>
seconds.` (30017) and
schedules the stop. The command returns without waiting for the countdown; console
input and gameplay remain available until the deadline. The delay starts after
the announcement is queued and is rounded up to the server timer resolution.

The server accepts one shutdown request. Further requests report an error without
changing the deadline or repeating the announcement. Seconds must be a whole number
from `0` to `2147483647`; negative, fractional, overflowing and extra arguments are
rejected. A scheduled shutdown survives the invoking player disconnecting.

The command stops this process, including both roles in Standalone mode. It uses the
same ordered cleanup as the host shutdown path: services stop, the final world save
completes, and persistence is disposed. It does not force-kill the process. Other
instances are unaffected. A manual host stop during the delay takes precedence and
the timer is discarded with the game loop. There is no cancel or restart subcommand.

## See also

- [All commands](../commands.md)
- [`save`](save.md)
