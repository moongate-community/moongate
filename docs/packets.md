# Packets and handlers

`Moongate.Network.Packets` defines wire formats independently of TCP and the game
server. `PacketRegistry` describes frames and decodes incoming packets;
`IPacketHandler<TPacket>` supplies synchronous game behavior, while
`IAsyncPacketHandler<TPacket>` handles packets that need I/O. These are separate
registrations. The initial built-in formats target **ClassicUO 7.x**.

## Built-in packet coverage

Lengths include the opcode and, for variable packets, the length header.
Directions are relative to the server. This is the default table, not the whole UO protocol:

| Opcode | Class | Direction | Length | Default host handler |
| --- | --- | --- | --- | --- |
| `0x55` | `LoginCompletePacket` | Outgoing | Fixed 1 | — |
| `0x73` | `PingPacket` | Both | Fixed 2 | Game: `PingPacketHandler`; Login: `LoginRolePingPacketHandler` |
| `0x80` | `AccountLoginPacket` | Incoming | Fixed 62 | Login: async account check, then `0xA8` list or `0x82` denial |
| `0x82` | `LoginDeniedPacket` | Outgoing | Fixed 2 | — |
| `0x8C` | `ServerRedirectPacket` | Outgoing | Fixed 11 | Login: sent after a valid `0xA0`, before closing the login connection |
| `0x91` | `GameLoginPacket` | Incoming | Fixed 65 | Game: validates and consumes the one-use handoff ticket |
| `0xA0` | `ServerSelectPacket` | Incoming | Fixed 3 | Login: checks realm eligibility, issues ticket and redirects |
| `0xA8` | `ServerListPacket` | Outgoing | Variable, minimum 6 | — |
| `0xB9` | `SupportFeaturesPacket` | Outgoing | Fixed 5 | — |
| `0xBD` | `ClientVersionPacket` | Incoming | Variable, minimum 4 | `ClientVersionPacketHandler` |
| `0xBD` | `ClientVersionRequestPacket` | Outgoing | Fixed 3 | — |
| `0xEF` | `LoginSeedPacket` | Incoming | Fixed 21 | `LoginSeedPacketHandler` |

The same opcode can have different definitions in each direction, as with `0xBD`.
The realm list is filtered by the authenticated account's minimum realm level.
It contains each realm's IPv4 address but no port. `0xA0` selects a live eligible
realm and `0x8C` supplies its IPv4 address, port and one-use key. The login sender
flushes `0x8C` before closing the login connection. On the new game connection
the client sends that key as a raw four-byte seed, followed by `0x91` with the
same key, username and password. The game checks the seed and atomically consumes
the Redis ticket. A direct `0xEF` client-version seed still works on game
listeners. Character selection and world entry are separate future work.

`TryGetDescriptor(opCode, out descriptor)` prefers incoming, then outgoing;
`descriptor.PacketType.Name` gives its class name. The overload accepting
`PacketDirection` selects one direction explicitly when needed.

`registry.TryDecode(bytes, out packet, out opCode)` always sets `opCode` to the
first byte, even on failure. Empty input returns false with opcode zero; inspect
input length to distinguish that case. Decoding needs one **complete incoming
frame**, including its header. It does not buffer a TCP stream. Outgoing-only
packets have metadata but no incoming parser.

## Define a packet and test its bytes

This example uses a private illustrative opcode. It is not an addition to the
ClassicUO protocol; use it only with a matching test client. Reference
`Moongate.Network.Packets` and place this in `ExamplePacket.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

[PacketHandler(0xFE, PacketSizing.Fixed, Length = 3)]
public sealed class ExamplePacket : BaseFixedPacket<ExamplePacket>,
    IIncomingPacket<ExamplePacket>, IOutgoingPacket
{
    public ushort Value { get; }

    public ExamplePacket(ushort value)
    {
        Value = value;
    }

    public static bool TryParse(ReadOnlySpan<byte> data,
        [NotNullWhen(true)] out ExamplePacket? packet)
    {
        packet = null;
        if (!HasValidHeader(data))
        {
            return false;
        }
        var reader = new PacketReader(data[1..]);
        if (!reader.TryReadUInt16BigEndian(out var value))
        {
            return false;
        }
        packet = new ExamplePacket(value);
        return true;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian(Value);
    }
}
```

The metadata attribute is named `PacketHandler`, but describes the **wire packet**;
it does not register the game handler class. Call `RegisterIncoming<T>()` for a
packet that implements `IIncomingPacket<T>` (a bidirectional packet covers both
directions in one call), or `RegisterOutgoing<T>()` for one that implements only
`IOutgoingPacket`. Duplicate types or conflicting opcode/direction pairs fail
registration. Freeze only after composing the table.

Run this `Program.cs` as a byte-level smoke test without a server or client:

```csharp
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Serialization;

var registry = new PacketRegistry();
PacketTable.Register(registry); // Optional: include the built-in formats.
registry.RegisterIncoming<ExamplePacket>();
registry.Freeze();

var bytes = PacketCodec.Encode(new ExamplePacket(0x1234));
if (!bytes.AsSpan().SequenceEqual(new byte[] { 0xFE, 0x12, 0x34 }) ||
    !registry.TryDecode(bytes, out var packet, out var opCode) ||
    packet is not ExamplePacket { Value: 0x1234 } || opCode != 0xFE)
{
    throw new InvalidOperationException("Packet round trip failed");
}
if (registry.TryDecode(new byte[] { 0xFE }, out _, out var failedOpCode) || failedOpCode != 0xFE)
{
    throw new InvalidOperationException("Malformed-frame opcode was lost");
}
Console.WriteLine("Packet bytes and failure opcode verified");
```

For variable packets, use `BasePacket<T>` with
`[PacketHandler(opcode, PacketSizing.Variable, MinimumLength = ...)]`, validate the
whole declared frame and write its complete length header. See the built-in
`ClientVersionPacket` and `ServerListPacket` for examples of parsing and writing.

## Register a game handler

Add a reference to `Moongate.Server.Core`. `ExamplePacketHandler.cs` can echo the
packet through the bounded sender without waiting for socket I/O:

```csharp
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;

public sealed class ExamplePacketHandler : IPacketHandler<ExamplePacket>
{
    private readonly IPacketSendService _sender;

    public ExamplePacketHandler(IPacketSendService sender)
    {
        _sender = sender;
    }

    public void Handle(GameSession session, ExamplePacket packet)
    {
        if (!_sender.TrySend(session.SessionId, new ExamplePacket(packet.Value)))
        {
            // The sender owns and observes connection cleanup.
            _ = _sender.DisconnectAsync(session.SessionId);
        }
    }
}
```

In `Program.cs` service composition, or a plugin's `Register` callback, import
`Moongate.Server.Core.Extensions` and call:

```csharp
container.RegisterPacketHandler<ExamplePacket, ExamplePacketHandler>();
```

Handlers are singletons. Keep per-player state in the session/world, not mutable
handler fields. `Handle` executes on the game loop; keep it short and synchronous.
`TrySend` snapshots encoded bytes and returns admission status, not delivery
confirmation. Decide what to do when it returns false; the example disconnects.

For a handler that awaits database or network I/O, implement
`IAsyncPacketHandler<TPacket>` and register it with
`RegisterAsyncPacketHandler<TPacket, THandler>()`. Its `HandleAsync` runs off
the game loop. The handler receives a `PacketContext` rather than a mutable
`GameSession`. This example assumes an application-specific lookup service:

```csharp
// Define this service in your plugin; keep its interface in a separate file.
public interface IExampleLookup
{
    Task<ushort?> LoadAsync(ushort value, CancellationToken cancellationToken);
}

public sealed class ExampleAsyncPacketHandler : IAsyncPacketHandler<ExamplePacket>
{
    private readonly IExampleLookup _lookup;

    public ExampleAsyncPacketHandler(IExampleLookup lookup)
    {
        _lookup = lookup;
    }

    public async ValueTask HandleAsync(
        PacketContext context,
        ExamplePacket packet,
        CancellationToken cancellationToken
    )
    {
        var value = await _lookup.LoadAsync(packet.Value, cancellationToken);

        if (value is not null)
        {
            context.TrySend(new ExamplePacket(value.Value));
        }
    }
}

container.RegisterAsyncPacketHandler<ExamplePacket, ExampleAsyncPacketHandler>();
```

When the result must change game state, return to the loop with
`await context.RunOnGameLoopAsync(session => { /* update session/world */ }, cancellationToken)`.
It returns `false` if the original session disconnected before the action ran.
An async handler must not mutate a session directly after an `await`.

Only one async packet may be in flight for a session. Until it finishes, the
dispatcher rejects further packets from that session; its executor accepts at
most 64 operations at once and runs at most four handlers concurrently.
Admission stays nonblocking. Disconnect and server shutdown cancel the
handler token; observe it in every awaited I/O call. Exceptions are logged
without packet payloads and do not stop the game loop.

**Host integration requires both registrations.** `PacketRegistry.Default` is
already frozen. The current host creates its UO framer and default game decoder
with that registry. Registering only a handler in a plugin does not add a new
opcode to the wire table. For a new built-in packet, extend `PacketTable` in the
source and register its handler in host composition. A custom host may supply its
own completed registry consistently to framing and decoding; changing just one
side is insufficient.

The host also registers LoginSeed and an async AccountLogin handler. The latter
checks credentials against `IAccountService`, sends `0x82` for denied login or
an empty eligible realm list, and sends a filtered `0xA8` list after success.
The login-only host uses a dedicated ordered async connection pipeline; standalone
runs separate login and game listeners. Selection and handoff use Redis-backed
leases and one-use tickets ([Implementation status](implementation-status.md)). See
[Transport and game ownership](network-game-separation.md) for connection lifecycle,
queue limits and overload policy, and [Game loop and timers](game-loop-and-timers.md)
for thread ownership and completion.
