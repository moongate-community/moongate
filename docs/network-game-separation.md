# Transport and game service ownership

The host separates TCP connection lifetime from game-session lifetime. `mode =
"login"` runs a dedicated ordered async login packet pipeline, Accounts access and
realm directory. `mode = "game"` runs the game loop, world services and Redis
realm presence. `standalone` combines account and game services and publishes its
local realm through Redis. Login and game own separate
connection registries, senders, packet dispatchers and TCP listeners. The defaults
are `network.login_port = 2593` and `network.game_port = 2595`; standalone rejects
the same port for both roles. With `network.listen_address = "0.0.0.0"`, each role
binds once per discovered local address, so standalone starts two listeners per
address. The local realm advertises `network.game_port` unless
`realm_directory.advertised_port` is set.

| Component | Responsibility |
| --- | --- |
| `ConnectionService` | Role-local membership, admission and complete connection closure |
| `NetworkService` | Listeners, per-connection protocol pipelines and synchronous transport notifications |
| `PacketSendService` | Encoded snapshots, bounded FIFO queues and owned outgoing I/O |
| `GameServerService` | Session creation, immediate packet decoding, dispatch and session retirement |
| `LoginServerService` | Independent login connections and ordered async packet handling |
| `RedisRealmDirectoryService` | Redis-backed live realm entries exposed to account login |
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
The game host supplies a new `GameSeedFramer` for each connection. It recognizes
a raw four-byte reconnect seed or a versioned `0xEF` seed packet, then delegates
subsequent packets to `UoPacketFramer`. Both framers use the packet registry the
host builds at startup. The game listener also installs `UoCompressionMiddleware`,
which Huffman-compresses outgoing data once the session enables compression after a
valid `0x91`. Options are snapshotted at construction, including endpoint objects.

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
5. Startup priorities are connection registries **40**, senders **50**, dispatchers
   **60**, game coordinator **100**, and standalone login coordinator **110**. The loop
   starts earlier. Each `NetworkService` is a plain singleton started by its role
   coordinator, so it is not registered for automatic startup a second time.
   Reverse shutdown keeps both roles' dependencies alive through session retirement.

The sender capacity defaults to 128 waiting encoded frames per connection, plus one
active write. It snapshots packets before a successful `TrySend` returns and preserves
FIFO order. Overflow or encoding/send failure closes admission and requests closure.
No new outbox can admit sends while closure is pending. The sender atomically captures
a per-registration owner-close signal through the three-argument `IConnectionService.TryGet`
overload. That signal survives registry removal and distinguishes an explicit live-connection
close from transport failure or remote completion. Cleanup failures are logged and
remain observable at shutdown; writes interrupted by a requested local close are expected.

## Upgrading from 0.1.x

Since 0.2.0 the `NetworkSession` constructor, the `NetworkSession.Client` return type
and the `ISessionService.GetOrCreate` parameter use `INetworkConnection` instead of
`MoongateTcpClient`. This is a binary API change: rebuild consumers and plugins.
Existing calls passing a concrete TCP client still compile. Custom session services
must change their method signature. Custom network services must implement the
three synchronous events `ConnectionAccepted`, `DataReceived` and `ConnectionClosed`.

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

`NetworkService` construction takes `(NetworkListenerOptions, IConnectionService)`;
`PacketSendService` takes `(IConnectionService, int capacity = 128)`. Custom hosts must
add `GameServerService` if they need the UO session/dispatch path. Registering only the raw
network service intentionally performs no packet decoding or game-session creation.

The login/game split also changes public service contracts for custom hosts and plugins.
Custom `IPacketSendService` implementations must implement
`SendAndDisconnectAsync(sessionId, expectedConnection, packet, cancellationToken)`:
send the final packet after queued frames, close that connection, and return `true`
only when the final packet reached the transport before closure. The old
`IRealmDirectoryService` is replaced by `IRealmCatalog` for asynchronous
`GetAvailableAsync`/`FindByIndexAsync` reads and `IRealmPresenceService` for
asynchronous `RegisterAsync`/`RenewAsync`/`UnregisterAsync` lease operations.
Standalone now publishes its realm through Redis instead of `RegisterLocal`.
