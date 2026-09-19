# Transport and game service ownership

The host separates TCP connection lifetime from game-session lifetime. This prepares
independent login and game services without adding login/account/realm behavior yet.
`MoongateServerConfig.Mode` still does not select runtime services in this phase.

| Component | Responsibility |
| --- | --- |
| `ConnectionService` | Role-local membership, admission and complete connection closure |
| `NetworkService` | Listeners, per-connection protocol pipelines and synchronous transport notifications |
| `PacketSendService` | Encoded snapshots, bounded FIFO queues and owned outgoing I/O |
| `GameServerService` | Session creation, immediate packet decoding, dispatch and session retirement |
| `PacketDispatchService` / `GameLoopService` | Typed handlers, ordered game work and loop-owned state mutation |

Transport and outgoing sends can run without `ISessionService` or `IGameLoopService`.
The implementations remain in the executable `Moongate.Server`; their public contracts
and event data live in the `Moongate.Server.Core` library.

## Compose a raw listener

```csharp
var connections = new ConnectionService();
var options = new NetworkListenerOptions
{
    Endpoints = [new IPEndPoint(IPAddress.Loopback, 2593)]
};
var network = new NetworkService(options, connections);

network.DataReceived += (_, args) =>
{
    // Copy inside the synchronous callback if another component needs the bytes later.
    byte[] ownedBytes = args.Data.ToArray();
    ProcessOwnedBytes(ownedBytes);
};

await connections.StartAsync();
try
{
    await network.StartAsync();
    await applicationShutdown;
}
finally
{
    try { await network.StopAsync(); }
    finally { await connections.StopAsync(); }
}
```

The snippet uses `System.Net`, `Moongate.Server.Core.Data.Network`, and
`Moongate.Server.Services.Network`. `ProcessOwnedBytes` and `applicationShutdown` are
application-provided behavior. No framing factory means raw TCP chunks, which are not
message boundaries. A protocol-specific listener supplies `ConnectionPipelineFactory`.
The game host supplies a new `UoPacketFramer` for each connection. Options are snapshotted
at construction, including endpoint objects.

## Callback and shutdown rules

1. Connection registration happens before `ConnectionAccepted`; game coordination creates
   its session in that callback. Receive callbacks decode packets before queueing them.
2. `DataReceived.Data` is valid only until the callback returns. Never queue that memory.
   A subscriber failure closes only its connection; close callbacks run independently so
   one failing subscriber cannot skip another subscriber's cleanup.
3. `DisconnectAsync` closes admission immediately. Its task joins the close request and
   actual completion. Never synchronously wait inside a transport callback or game handler.
   The registry/sender retains cleanup ownership even when the caller cannot await it.
4. Game shutdown closes transport first, then joins both sender cleanup and loop-owned
   session retirement. Both operations start even if either throws. All listeners are
   stopped after partial startup failure, while the original startup exception is preserved.
5. Startup priorities are connection registry **40**, sender **50**, dispatcher **60**,
   game coordinator **100**. The loop starts earlier. `NetworkService` is a plain singleton
   started by the coordinator, so it is not registered for automatic startup a second time.
   Reverse shutdown keeps all dependencies alive through session retirement.

The sender capacity defaults to 128 waiting encoded frames per connection, plus one
active write. It snapshots packets before a successful `TrySend` returns and preserves
FIFO order. Overflow or encoding/send failure closes admission and requests closure.
No new outbox can admit sends while closure is pending. Cleanup failures are logged and
remain observable at shutdown; writes interrupted by a requested local close are expected.

## Migrate consumers and plugins

Rebuild all `Moongate.Server.Core` consumers: the `NetworkSession` constructor,
`NetworkSession.Client` return type, and `ISessionService.GetOrCreate` parameter now use
`INetworkConnection` instead of `MoongateTcpClient`. Existing calls passing a concrete
TCP client still compile. Custom session services must change their method signature.
Custom network services must implement the three new synchronous events.

```csharp
// Before: session.NetworkSession.Client?.Dispose();
await connectionService.DisconnectAsync(session.SessionId);
// Or, when outgoing work must also be joined:
await packetSendService.DisconnectAsync(session.SessionId);
```

Inside a synchronous packet handler, request the sender's owned cleanup without blocking:
`_ = packetSendService.DisconnectAsync(session.SessionId);`.

`NetworkSession.DetachClient()` only detaches game metadata and preserves endpoint
snapshots; it does not close a connection. Its disconnected state remains terminal.
`INetworkConnection.LocalEndPoint` is optional and defaults to null for existing custom
implementations. Always handle absent local metadata.

`NetworkService` construction now takes `(NetworkListenerOptions, IConnectionService)`;
`PacketSendService` takes `(IConnectionService, int capacity = 128)`. Custom hosts must
add `GameServerService` if they need the UO session/dispatch path. Registering only the raw
network service intentionally performs no packet decoding or game-session creation.
