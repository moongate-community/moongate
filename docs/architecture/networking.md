# Networking

`NetworkService` is the boundary between framed TCP input and session-aware packet handlers. It owns listener setup and dispatch policy while packet types own their wire parsing and serialization.

## Connection and framing

On startup, every `IPacketHandlerRegistration` registers itself. `RegisterHandler<TPacket>` indexes a 256-entry dispatch table by `TPacket.PacketId`; duplicate registrations fail immediately. A registered delegate constructs a `SpanReader`, calls the packet type's static `Read`, and invokes the handler with a `PacketContext` containing the session.

The listener is configured with a connection-pipeline factory. Each accepted connection therefore receives a fresh, stateful `UoSeedFramer`. Sharing this framer would be incorrect because it remembers whether that connection's opening seed has been resolved.

`UoSeedFramer` handles the two legal openings:

- A login connection begins with a declared login-seed packet (`0xEF`), which is framed as an ordinary packet.
- A game-server reconnect begins with a raw four-byte seed without a packet id. The framer emits those four bytes as one frame and then delegates subsequent input to `UoPacketFramer`.

`UoPacketFramer` looks up fixed lengths in `PacketLengths`. For variable packets, it reads the big-endian total length from bytes 1–2. It waits for incomplete input, returns only the first complete frame from coalesced input, and throws `UoFramingException` for unknown ids or variable lengths smaller than the three-byte header.

## Transport-to-game dispatch

```text
transport data event
  -> find or create PlayerSession
  -> copy reusable receive data to byte[]
  -> post work to IMainThreadDispatcher
  -> process seed handshake
  -> look up handler by first byte
  -> parse packet and run handler
  -> publish PacketDispatchedEvent on success
```

The receive callback copies `e.Data` because the transport reuses that buffer and because a span cannot be captured by the posted closure. `NetworkService` then posts `ProcessInbound` to `IMainThreadDispatcher`. This establishes the documented Moongate guarantee: session/game-state work initiated by inbound packets runs through the main game-loop dispatcher. The transport's own threading model is not described here.

While a session is awaiting its seed, `SeedHandshake` accepts the declared login-seed packet without consuming it, or captures and consumes a non-zero raw four-byte seed. Empty, short, or zero raw seeds are rejected and the connection is closed. Once the session has advanced, frames pass through unchanged.

If no handler is registered for a framed opcode, the service logs that the packet is not implemented and returns; it neither publishes `PacketDispatchedEvent` nor closes the session. Registered handlers are invoked inside a catch boundary. Successful invocation publishes the event, while an exception is logged and contained.

## Sessions, output, and compression

Connection and disconnection callbacks create/remove `PlayerSession` entries and publish `SessionCreatedEvent`/`SessionDestroyedEvent`. A session adds its compression middleware when constructed, but compression remains disabled until game-server login succeeds. The game-server login handler enables it before sending support features and the character list, so the earlier login-server exchange is sent uncompressed.

`PlayerSession.Send` serializes an outgoing packet into a growable buffer on the calling thread, copies the bytes, and starts the client send asynchronously. Moongate does not wait for socket I/O in the packet handler. Details of transport send synchronization are not documented beyond the interface usage visible here.

## Source map

### Runtime

- `src/Moongate.Server/Services/Network/NetworkService.cs`
- `src/Moongate.Network/Framing/UoSeedFramer.cs`
- `src/Moongate.Network/Framing/UoPacketFramer.cs`
- `src/Moongate.Server/Services/Network/SeedHandshake.cs`
- `src/Moongate.Server/Data/Session/PlayerSession.cs`
- `src/Moongate.Server/Services/Accounts/SessionManager.cs`

### Tests

- `tests/Moongate.Tests/Network/UoSeedFramerTests.cs`
- `tests/Moongate.Tests/Network/UoPacketFramerTests.cs`
- `tests/Moongate.Tests/Server/SeedHandshakeTests.cs`
- `tests/Moongate.Tests/Server/LoginFlowIntegrationTests.cs`
