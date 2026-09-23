![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Network

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

See the [standalone TCP cookbook](https://moongate.sh/libraries/network-cookbook/)
for a runnable framed client/server exchange, per-connection middleware and cleanup.

## Dependencies and scope

This package depends on Serilog and has no dependency on other Moongate packages.

TCP is a byte stream. Configure framing for your application protocol; transport reads do not define game-packet boundaries.
This library does not automatically decode Ultima Online packets or dispatch work into a game loop. Use
`Moongate.Network.Packets` for UO packet definitions and serialization.

## Abstract connection metadata

`INetworkConnection` exposes transport identity, completion, send/close operations and
remote endpoint metadata. Its optional `LocalEndPoint` property has a default interface
implementation returning null, so existing custom connection implementations do not need
a new member. `MoongateTcpClient` supplies its actual local endpoint.

```csharp
string DescribeLocalEndpoint(Moongate.Network.Interfaces.Client.INetworkConnection connection)
{
    return connection.LocalEndPoint?.ToString() ?? "Local endpoint unavailable";
}
```

TCP event payloads are stable copies; middleware inputs are borrowed until their
`ValueTask` completes. Connection ownership above the transport (game sessions,
`IConnectionService`, `INetworkService`) lives in `Moongate.Server.Core`, not here.

## Configured entry points

From 0.4.0, `MoongateTcpServer.CreateConfigured(endpoint, options)` and
`MoongateTcpClient.ConnectConfiguredAsync(options)` prepare a transport stream and
install callbacks before reception starts. `ConnectionPipeline.PrepareStreamAsync` can
wrap the socket stream, for example to authenticate an `SslStream`, and
`ConfigureClient` subscribes to receive events before the first byte is delivered.
The rules:

- The preparation token covers the connection/setup deadline and caller cancellation;
  preparation callbacks must observe it.
- The returned stream must own its input stream. If preparation throws, dispose any
  wrapper you created before throwing. After successful setup the transport owns the
  prepared stream and the socket, and a configuration failure releases both.
- `TcpServerOptions` bounds admitted connections and concurrent preparations; excess
  sockets close immediately. A slow TLS handshake does not block accepting another
  connection. Server stop cancels preparation and drains the admitted setups before
  a restart.

The plain constructor and `ConnectAsync` have no preparation step and keep their
original admission behavior.

## Graceful shutdown

For graceful application shutdown, `StopAcceptingAsync()` closes the listener and cancels unfinished stream preparation while
established connections remain usable. `IsRunning` becomes false during this drain, while `Endpoint` retains a safe snapshot
of the bound address and port. Drain application work, then call `StopAsync()` or `DisposeAsync()`. A new listener generation
requires a complete stop before restart.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).
