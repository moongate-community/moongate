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

See [Packets and handlers](https://moongate-community.github.io/moongate/server/packets/)
for the complete built-in opcode table, a custom packet, byte-level tests and host integration.

## Binary span utilities

These additional helpers were introduced on `develop` after version 0.4.0. Use a
source reference or a release containing the addition.

`Moongate.Network.Packets.Spans` also exposes `SpanReader`, `SpanWriter`, and `SpanOwner`
for standalone binary serialization. They support signed and unsigned primitives,
big- and little-endian values, ASCII/UTF-8/UTF-16 strings, seeking, and packet-length
patching. `SpanReader` provides throwing reads and strict `TryRead` methods.

For generic string reads and writes, `fixedLength` describes a field in encoding
units: bytes for ASCII/UTF-8, 16-bit units for UTF-16, and 32-bit units for UTF-32.
The legacy writer truncates the input to at most `fixedLength` UTF-16 code units,
then encodes and zero-pads the field. If that prefix exceeds the encoded field
width, it throws before modifying the destination.

Construct a `SpanWriter` with a caller-owned `Span<byte>` for a fixed buffer, or with
an initial capacity to rent a pooled buffer. Pass `resize: true` to enable growth.
`EnsureCapacity` reserves total capacity; `EnsureRemainingCapacity` reserves space
after the current cursor. `ToArray` copies the written payload; `ToSpan` transfers a
pooled buffer to a disposable `SpanOwner` (or copies a caller-owned buffer) and resets
the writer.

Dispose writers in a `finally` block and dispose each returned owner after use.
Pass pooled writers by `ref`: copying them would duplicate ownership of the same
buffer. Do not retain spans across growth, ownership transfer, or disposal.

Existing packet implementations continue to use `PacketReader` and `PacketWriter`.
Their public signatures and fixed-buffer behavior are unchanged; use the standalone
helpers when writing binary data outside those packet contracts.

## Dependencies and scope

This package depends on `Moongate.Core`. It does not require `Moongate.Network` to encode or decode packets.

The packet set is a subset of the UO protocol. Decoding expects a complete packet frame; buffering and framing a TCP stream belong to the transport integration. Packet handlers and game-loop dispatch belong to the server integration.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).
