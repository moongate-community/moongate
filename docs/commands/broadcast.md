# broadcast

Sends a system message to every player in this world.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `broadcast <text>` | Yes | Yes | Administrator | Game |

```text
broadcast Server maintenance in five minutes.
```

In game, administrators use `.broadcast Server maintenance in five minutes.`.
The text is delivered as a Unicode system chat message to connected characters
currently in the world on this instance, regardless of map or distance. Character
selection sessions and disconnected clients are excluded. Other server instances
do not receive the message.

Text after the command name is sent without needing quotes. Leading and trailing
whitespace is trimmed; spaces inside the message are preserved. Empty input prints
usage and sends nothing. The caller receives the number of players whose outgoing
queues accepted the message; this is not a client receipt acknowledgment. In-game
input retains the existing 128-character speech limit, including the dot and command.
Console messages must fit both the Unicode speech packet and the compressed transport
limit. Oversized messages are rejected before any player receives them.

## See also

- [All commands](../commands.md)
