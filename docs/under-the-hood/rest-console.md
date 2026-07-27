# Console over REST

A REST web-terminal onto the admin command set: open a stream to receive output, then POST
commands to it. Both endpoints require a staff JWT (`Administrator` or `GrandMaster`) — the same
`admin` policy as the rest of the admin API.

It is the HTTP twin of the [admin console](admin-console.md): a command runs through the same
`ICommandService` on the game loop, and its reply lines are delivered through a callback — here,
onto a per-connection [Server-Sent Events](https://developer.mozilla.org/docs/Web/API/Server-sent_events)
feed instead of a telnet socket.

## Open a stream

`GET /api/v1/admin/console/stream` returns a `text/event-stream`. The **first** event is `ready`,
whose data is a **connection id** — keep it to send commands on this stream:

```
event: ready
data: 3f2b8c…

event: line
data: Broadcast sent.

event: done
data: broadcast hello
```

The stream stays open until the client disconnects; each command you send produces one `line`
event per reply line, then a `done` event carrying the command that finished.

## Send a command

`POST /api/v1/admin/console` with the connection id from the stream:

```json
{ "command": "broadcast hello", "connectionId": "3f2b8c…" }
```

It returns **202 Accepted** immediately; the command is dispatched onto the game loop and its
output arrives asynchronously on that connection's stream. A POST with an unknown or closed
connection id returns **404**.

## Which commands are reachable

A command is reachable over REST only if its registration opts into the `Rest`
[source](admin-console.md) — network exposure is curated separately from the loopback telnet
console. `broadcast` opts in. Unknown or unavailable commands reply `Unknown command.`, exactly as
they do in-game and on the telnet console.
