![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Network

`MoongateTcpClient.ConnectConfiguredAsync` accepts `TcpClientOptions` to prepare
a transport stream and install callbacks before reception starts. For example,
`ConnectionPipeline.PrepareStreamAsync` can authenticate an `SslStream`, while
`ConfigureClient` subscribes to receive events before the first byte is delivered.
The preparation token covers the connection/setup deadline and caller cancellation.
The returned stream must own its input stream; if preparation throws, dispose any
wrapper created before throwing. After successful setup, the client owns both the
prepared stream and socket. Configuration failure also releases both resources.
The existing `ConnectAsync` overload remains available with its original behavior.

Use `MoongateTcpServer.CreateConfigured(endpoint, options)` for the corresponding
accepted-connection preparation path. `TcpServerOptions` bounds both admitted
connections and concurrent preparation operations; excess sockets close immediately.
A slow TLS handshake does not block accepting another connection. Server stop cancels
preparation, drains the admitted setups and owns their socket cleanup before a restart.
Preparation callbacks must observe cancellation. The legacy constructor retains its
original admission behavior; preparation callbacks are enabled by the configured entry points.

Standalone asynchronous TCP transport with framing, middleware, and per-connection pipelines for .NET applications.

## Installation

Requires .NET 10. Use the package version available in your configured NuGet feed.

```shell
dotnet add package Moongate.Network
```

## Features

- Asynchronous TCP server and client APIs.
- Access to the bound server endpoint, including an automatically assigned port.
- Configurable framing, transport codecs, and middleware.
- Per-connection pipelines for application-specific connection handling.

## Example

Start a loopback listener on an available port and stop it cleanly:

<!-- nuget-smoke:Program.cs -->
```csharp
using System.Net;
using Moongate.Network.Server;

await using var server = new MoongateTcpServer(new IPEndPoint(IPAddress.Loopback, 0));
await server.StartAsync(CancellationToken.None);

if (server.Endpoint.Port == 0)
{
    throw new InvalidOperationException("The listener did not bind to a port.");
}

await server.StopAsync(CancellationToken.None);
Console.WriteLine("TCP listener started and stopped.");
```

## Dependencies and scope

This package depends on Serilog and has no dependency on other Moongate packages.

TCP is a byte stream. Configure framing for your application protocol; transport reads do not define game-packet boundaries. This library does not automatically decode Ultima Online packets or dispatch work into a game loop. Use `Moongate.Network.Packets` for UO packet definitions and serialization.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).

For graceful application shutdown, `StopAcceptingAsync()` closes the listener and cancels unfinished stream preparation while established connections remain usable. Drain application work, then call `StopAsync()` or `DisposeAsync()`. A new listener generation requires a complete stop before restart.
