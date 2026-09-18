![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Network.Packets

Ultima Online packet definitions, span-based serialization, and packet registration for Moongate.

## Installation

Requires .NET 10. Use the package version available in your configured NuGet feed.

```shell
dotnet add package Moongate.Network.Packets
```

## Features

- An initial packet set targeting ClassicUO 7.x, including login, control, ping, and supported-feature messages.
- A packet registry using packet metadata for registration and opcode lookup.
- Span-based readers and writers for packet serialization.
- Encoding and decoding APIs that can be tested independently of a TCP server.

## Example

Register the ping packet, freeze the registry, and decode an encoded frame:

<!-- nuget-smoke:Program.cs -->
```csharp
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Serialization;

var registry = new PacketRegistry();
registry.RegisterPacket<PingPacket>();
registry.Freeze();

var bytes = PacketCodec.Encode(new PingPacket(42));

if (!registry.TryDecode(bytes, out var packet, out var opCode)
    || packet is not PingPacket ping)
{
    throw new InvalidOperationException($"Could not decode opcode {opCode:X2}.");
}

Console.WriteLine($"{opCode:X2}:{ping.Sequence}");
```

## Dependencies and scope

This package depends on `Moongate.Core`. It does not require `Moongate.Network` to encode or decode packets.

The packet set is a subset of the UO protocol. Decoding expects a complete packet frame; buffering and framing a TCP stream belong to the transport integration. Packet handlers and game-loop dispatch belong to the server integration.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).
