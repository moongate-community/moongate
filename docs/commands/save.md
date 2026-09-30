# save

Saves the world and tells every player when it is done.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `save` | Yes | Yes | Administrator | Game |

```text
save
```

In game, administrators use `.save`. The command requests a save through the existing
world save coordinator and waits for durable persistence to finish. A request made
during another save joins that save rather than starting a competing operation.
Each successful command broadcasts `The world has been saved in <seconds> seconds.`
(message 30015, in the server language) to connected characters currently in the
world on this instance, across all maps. The console also prints the same completion
message; an in-game caller receives it through the broadcast. The elapsed time
measures the wait for saving, excluding broadcast delivery, in seconds with two
decimals (for example, `The world has been saved in 1.23 seconds.`).

A failed save produces an error for the caller and no success broadcast. Extra
arguments print usage without saving. Automatic and shutdown saves keep their
existing behavior; this announcement belongs to the `save` command.

## See also

- [All commands](../commands.md)
- [`shutdown`](shutdown.md)
