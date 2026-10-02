# Standalone TCP cookbook

`Moongate.Network` is a standalone .NET 10 transport library. It has no dependency
on game sessions, UO packets or the game loop. This guide uses a tiny protocol
whose frames are exactly two bytes. Reference the `Moongate.Network` package.

## Frame boundaries

TCP can split one message or combine several messages in a read. Put this framer
in `PairFramer.cs`; the transport retains incomplete data and calls it again:

```csharp
using Moongate.Network.Interfaces.Framing;

public sealed class PairFramer : INetFramer
{
    public bool TryReadFrame(Span<byte> buffer, out int frameLength)
    {
        frameLength = 2;
        return buffer.Length >= frameLength;
    }
}
```

A real variable-length framer must validate the header and declared size. Return
false for incomplete input; throw for malformed input. A reported frame length
must be positive and no greater than available bytes and `MaxFrameLength`.
A framer may transform data in place, but must transform each byte at most once
across repeated calls. Supply stateful framers fresh for each connection.

## Middleware

`INetMiddleware` sees raw chunks **before** inbound framing, so it must not assume
one call equals one message. This pass-through example goes in `PassThroughMiddleware.cs`:

```csharp
using Moongate.Network.Client;
using Moongate.Network.Interfaces.Middleware;

public sealed class PassThroughMiddleware : INetMiddleware
{
    public ValueTask<ReadOnlyMemory<byte>> ProcessAsync(MoongateTcpClient? client,
        ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(data);
    }

    public ValueTask<ReadOnlyMemory<byte>> ProcessSendAsync(MoongateTcpClient? client,
        ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(data);
    }
}
```

Returning empty memory drops the payload and stops the remaining pipeline.
Middleware input is borrowed until its returned `ValueTask` completes. Do not
retain it or return memory whose owner has been disposed. Receive calls are serial
per connection, as are sends, but the two directions may run concurrently; keep
mutable inbound and outbound state separate. Never call the same client's
`SendAsync` inside `ProcessSendAsync`: its send lock is not reentrant.

## Connect, exchange a frame and stop

This `Program.cs` installs callbacks before receive starts, copies data for the
external continuation, and performs sends outside event callbacks. Every operation
wait has a deadline; disposal remains responsible for draining the transport.

```csharp
using System.Net;
using Moongate.Network.Client;
using Moongate.Network.Data;
using Moongate.Network.Data.Config;
using Moongate.Network.Server;

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var token = timeout.Token;
var received = new TaskCompletionSource<(MoongateTcpClient Client, byte[] Bytes)>(
    TaskCreationOptions.RunContinuationsAsynchronously);
var reply = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

await using var server = MoongateTcpServer.CreateConfigured(
    new IPEndPoint(IPAddress.Loopback, 0),
    new TcpServerOptions
    {
        MaxFrameLength = 2,
        ConnectionPipelineFactory = () => new ConnectionPipeline
        {
            Framer = new PairFramer(),
            Middlewares = [new PassThroughMiddleware()],
            ConfigureClient = connection =>
            {
                connection.OnDataReceived += (_, e) =>
                    received.TrySetResult((e.Client, e.Data.ToArray()));
                connection.OnException += (_, e) => received.TrySetException(e.Exception);
            }
        }
    });
await server.StartAsync(token);

await using var client = await MoongateTcpClient.ConnectConfiguredAsync(
    server.Endpoint,
    new TcpClientOptions
    {
        MaxFrameLength = 2,
        Pipeline = new ConnectionPipeline
        {
            Framer = new PairFramer(),
            ConfigureClient = connection =>
            {
                connection.OnDataReceived += (_, e) => reply.TrySetResult(e.Data.ToArray());
                connection.OnException += (_, e) => reply.TrySetException(e.Exception);
            }
        }
    }, token);

// Two separate writes deliberately exercise stream reassembly.
await client.SendAsync(new byte[] { 0x12 }, token);
await client.SendAsync(new byte[] { 0x34 }, token);
var request = await received.Task.WaitAsync(token);
await request.Client.SendAsync(request.Bytes, token);
var response = await reply.Task.WaitAsync(token);
if (!response.AsSpan().SequenceEqual(new byte[] { 0x12, 0x34 }))
{
    throw new InvalidOperationException("TCP round trip failed");
}
await client.CloseAsync(token);
await server.StopAsync(token);
Console.WriteLine("Framed TCP exchange verified");
```

`Endpoint` exposes the bound address and assigned port after start, including when
port zero requests an ephemeral port. The example opens only loopback.
`ConnectConfiguredAsync` installs `ConfigureClient` after stream preparation but
before reception. `ConnectAsync` starts reception before returning, so subscribing
after that call can miss an early response.

There are two different memory contracts: the TCP library's
`TcpDataReceivedEventArgs.Data` is a stable copy and may be retained. Middleware
buffers are borrowed. The host's `INetworkService.DataReceived` contract is also
borrowed until the callback returns, even when a particular transport currently
uses copied storage. Decode or copy at that host boundary before queueing work.
The explicit copies above are safe for either ownership pattern.

## Configuration and ownership

Configured listeners default to 32 admitted connections, eight concurrent stream
preparations and a five-second preparation deadline. Client/server defaults use
an 8192-byte receive buffer, a 1 MiB maximum frame and `NoDelay = true`. Excess
admissions close immediately. A per-connection `ConnectionPipelineFactory` avoids
sharing cipher, codec, middleware or framing state across connections.

`PrepareStreamAsync` can wrap the stream (for example, an authenticated TLS stream).
Its returned readable/writable stream must own the input. Observe the supplied
cancellation token and dispose any wrapper you create if preparation fails. After
successful setup, transport owns the prepared stream and socket. Preparation runs
only through the configured entry points, `MoongateTcpServer.CreateConfigured` and
`MoongateTcpClient.ConnectConfiguredAsync` (from 0.4.0); the plain constructor and
`ConnectAsync` have no preparation step. The server uses the transport for UO
client listeners; realm coordination is backed by Redis.

For graceful shutdown, stop producers and call `StopAcceptingAsync()` to close the
listener/cancel unfinished setup while established connections remain available.
Drain application work, then call `StopAsync()` or dispose the server to join
connection cleanup. A restart needs the full stop to finish. Never synchronously
wait for connection cleanup inside receive/disconnect events; callbacks are part
of the work that cleanup waits for. `Completion` represents full connection
cleanup; `IsConnected == false` or a close event alone does not.

This framing sample does not parse UO packets. See [Packets and handlers](packets.md)
and [Transport and game ownership](network-game-separation.md) for host integration.


## UO host encryption middleware

The UO host installs a fresh `UoEncryptionMiddleware` through
`UoNetworkOptionsFactory` for each accepted login/game connection when
`network.encryption.mode` is `Optional` or `Required`. See
[client encryption configuration](server-configuration.md#uo-client-encryption).

Incoming bytes pass through decryption before `UoPacketFramer` or `GameSeedFramer`.
The middleware retains at most 86 handshake bytes: a four-byte raw seed or 21-byte
`0xEF` seed, followed by the complete 62-byte account login or 65-byte game login.
It first checks for plaintext using the canonical `AccountLoginPacket.TryParse`
or `GameLoginPacket.TryParse` contract, including credential validation. `Optional`
accepts a valid plaintext login; `Required` rejects it. Otherwise, it decrypts with
the configured profile and requires the same complete packet validation before
releasing any seed or login frames. Full-width ASCII credentials and padding after
a NUL follow the normal packet parser rules.

Fragmented handshakes wait for more input; bytes coalesced after the login continue
through the established cipher without decrypting a byte twice. On the login
listener, raw legacy seeds become `0xEF` frames using the version components from
`network.encryption.client_version`. Game seed bytes and existing versioned seeds
remain intact; their version fields do not select another encryption profile.
Zero seeds and the legacy KR `0xFFFFFFFF` seed handshake are rejected.

Outgoing game data runs through `UoCompressionMiddleware` first and the encryption
middleware second; compression starts only when enabled for the session.
POL profiles cover the old, 1.25.36 and standard login XOR variants, Blowfish game
streams, combined Blowfish/Twofish, and Twofish with server-to-client MD5-XOR.
Only the Twofish profile transforms outgoing game bytes; the Blowfish and combined
profiles leave them unencrypted, as POL does. Login-server replies remain plaintext.
Cipher state belongs to the connection, with independent inbound and outbound
positions. The transport serializes send transformations together with socket
writes, preserving cipher order even when callers send concurrently.

A rejected seed or login throws before any handshake frames are dispatched, and
the transport closes the connection. This is structural validation, not
authenticated encryption: a wrong profile can theoretically produce a valid packet.
The listener uses one configured profile, without automatic client-version
detection or fallback to other profiles. POL vector and loopback tests cover the
implemented streams, fragmentation, coalescing and connection isolation. A live
Enhanced Client 4.0.117 logs in with the `67.0.117.0` profile; see
[Enhanced Client](enhanced-client.md) for what works beyond the login.

Golden vectors under `tests/Moongate.Network.Packets.Tests/TestSupport/Encryption`
come from the original POL C++ algorithms. To regenerate them from a POL checkout:

```sh
python3 tests/Moongate.Network.Packets.Tests/TestSupport/Encryption/generate-pol-vectors.py /path/to/polserver
```

The generator requires Python 3, Git and a C++20 `g++` compiler. It rewrites
`pol-vectors.json` and records the source checkout's commit. Production uses C#
cipher implementations and .NET's MD5 API, without a POL binary dependency.
