# Packet handlers

A **packet handler** reacts to a decoded packet the client sends. A plugin can register one
for any inbound opcode, exactly the way the server registers its own.

## Two interfaces, one class

A handler implements two interfaces on the same class:

- `IPacketHandler<TPacket>` — `void Handle(TPacket packet, in PacketContext context)` — the
  logic that runs when a `TPacket` arrives.
- `IPacketHandlerRegistration` — `void Register(INetworkService network)` — how the handler
  attaches itself to the network service. The body is always `network.RegisterHandler(this)`.

This is the shape of every built-in handler, such as the ping/keep-alive handler:

```csharp
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Network;

namespace MyShard.Net.Handlers;

/// <summary>Echoes the client's keep-alive sequence (0x73) back to it.</summary>
public sealed class MyPingHandler : IPacketHandler<PingPacket>, IPacketHandlerRegistration
{
    public void Handle(PingPacket packet, in PacketContext context)
        => context.Session.Send(new PingAckPacket(packet.Sequence));

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
```

## Registering

Register the handler in your plugin's `Configure`:

```csharp
container.RegisterPacketHandler<MyPingHandler>();
```

The network service collects every registration and wires each to its packet's opcode at
startup.

## Threading

`Handle` runs on the network thread. Reply to the client directly with `context.Session.Send(...)`.
For anything that mutates world state, marshal onto the game loop rather than touching entities
from the network thread.

## `[PacketDocumentation]` is not yours

The `[PacketDocumentation]` attribute and the packet-docs generator apply to Moongate's *own*
packet classes — the definitions of the packets themselves. A plugin **consumes** an existing
packet type from `Moongate.Network`; it does not define one, so it does not use that attribute.

> The example above re-registers an opcode the core already handles, purely to show the seam.
> Real plugins usually handle opcodes the core leaves open — registering a second handler for an
> opcode the server already owns is for illustration, not production.
